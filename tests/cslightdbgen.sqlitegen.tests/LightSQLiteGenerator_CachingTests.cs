using System.Collections.Immutable;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using CsLightDbGen.SQLiteGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using GeneratorRunResult = cslightdbgen.sqlitegen.tests.TestInfrastructure.GeneratorRunResult;

namespace cslightdbgen.sqlitegen.tests;

/// <summary>
/// Verifies the incremental-generator caching contract established by the
/// <see cref="LightSQLiteGenerator"/> pipeline: because each target is projected onto a fully
/// value-equatable model (<c>TableModel</c> / <c>FtsModel</c>), editing unrelated code must not
/// re-run the transform for a model whose own source is unchanged.
/// </summary>
public class LightSQLiteGenerator_CachingTests
{
    private const string TableSource = """
        using CsLightDbGen.SQLiteGenerator;

        namespace Demo;

        [LdgSQLiteTable("widgets")]
        public partial class Widget
        {
            [LdgSQLiteKey]
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }
        """;

    private const string FtsSource = """
        using CsLightDbGen.SQLiteGenerator;

        namespace Demo;

        [LdgSQLiteTable("widgets")]
        public partial class WidgetSource
        {
            [LdgSQLiteKey]
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }

        [LdgSQLiteFtsTable("widgets")]
        public partial class WidgetSearch
        {
            public string Name { get; set; } = string.Empty;

            public string Body { get; set; } = string.Empty;
        }
        """;

    private const string UnrelatedSource = """
        namespace Unrelated
        {
            public class Bystander
            {
                public int X { get; set; }
            }
        }
        """;

    [Fact]
    public void IncrementalPipeline_CachesUnchangedTableModel_WhenUnrelatedTreeAdded()
    {
        GeneratorDriverRunResult secondRun =
            GeneratorTestHost.RunAndReRunWithUnrelatedTree(UnrelatedSource, TableSource);

        AssertAllStepOutputsReused(secondRun, LightSQLiteGenerator.TableModelTrackingName);
    }

    [Fact]
    public void IncrementalPipeline_CachesUnchangedFtsModel_WhenUnrelatedTreeAdded()
    {
        GeneratorDriverRunResult secondRun =
            GeneratorTestHost.RunAndReRunWithUnrelatedTree(UnrelatedSource, FtsSource);

        AssertAllStepOutputsReused(secondRun, LightSQLiteGenerator.FtsModelTrackingName);
    }

    private const string EnvironmentSource = """
        #if CSLIGHTDBGEN_MULTI_HOST
        [assembly: CsLightDbGen.SQLiteGenerator.LdgSQLiteRuntimeNamespace("Caching.HostA.Runtime")]
        #endif
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Environment_UnrelatedEditReusesEffectiveOutputs(bool local)
    {
        GeneratorRunResult first = GeneratorTestHost.Run(
            new GeneratorTestOptions
            {
                AssemblyName = "EnvironmentCachingHost",
                ParseOptions = local
                    ? CSharpParseOptions.Default.WithPreprocessorSymbols("CSLIGHTDBGEN_MULTI_HOST")
                    : CSharpParseOptions.Default,
                TrackIncrementalSteps = true,
            },
            EnvironmentSource, FtsSource, UnrelatedSource);
        GeneratorRunResult second = GeneratorTestHost.ReRun(first,
            new Dictionary<int, string> { [2] = UnrelatedSource.Replace("int X", "long X", StringComparison.Ordinal) });

        AssertClean(first);
        AssertClean(second);
        AssertAllStepOutputsReused(second.DriverRunResult, LightSQLiteGenerator.TableModelTrackingName);
        AssertAllStepOutputsReused(second.DriverRunResult, LightSQLiteGenerator.FtsModelTrackingName);
        AssertAllStepOutputsReused(second.DriverRunResult, LightSQLiteGenerator.EnvironmentTrackingName);
        foreach (string name in EffectiveEmissionTrackingNames)
        {
            AssertAllStepOutputsReused(second.DriverRunResult, name);
            AssertSourceOutputReasons(second.DriverRunResult, name, changed: false);
        }
        AssertSameSources(first, second);
    }

    [Fact]
    public void Environment_NamespaceOrSymbolChangeInvalidatesTableFtsAndRuntime()
    {
        GeneratorRunResult defaults = GeneratorTestHost.Run(
            new GeneratorTestOptions { AssemblyName = "ChangingEnvironmentHost", TrackIncrementalSteps = true },
            EnvironmentSource, FtsSource);
        GeneratorRunResult localA = GeneratorTestHost.ReRun(defaults,
            parseOptions: CSharpParseOptions.Default.WithPreprocessorSymbols("CSLIGHTDBGEN_MULTI_HOST"));
        AssertEnvironmentChange(defaults, localA, "Caching.HostA.Runtime");

        GeneratorRunResult localB = GeneratorTestHost.ReRun(localA,
            new Dictionary<int, string>
            {
                [0] = EnvironmentSource.Replace("Caching.HostA.Runtime", "Caching.HostB.Runtime", StringComparison.Ordinal),
            });
        AssertEnvironmentChange(localA, localB, "Caching.HostB.Runtime");

        GeneratorRunResult restoredDefault = GeneratorTestHost.ReRun(localB, parseOptions: CSharpParseOptions.Default);
        AssertEnvironmentChange(localB, restoredDefault, "CsLightDbGen.SQLiteGenerator");
        AssertSameSources(defaults, restoredDefault);
    }

    [Fact]
    public void Environment_InvalidHostRemovesOrdinaryOutputs_AndValidHostRestoresThem()
    {
        GeneratorRunResult valid = GeneratorTestHost.Run(
            new GeneratorTestOptions
            {
                AssemblyName = "GatedEnvironmentHost",
                ParseOptions = CSharpParseOptions.Default.WithPreprocessorSymbols("CSLIGHTDBGEN_MULTI_HOST"),
                TrackIncrementalSteps = true,
            },
            EnvironmentSource, FtsSource);
        GeneratorRunResult invalid = GeneratorTestHost.ReRun(valid,
            new Dictionary<int, string> { [0] = string.Empty });
        invalid.DriverRunResult.Diagnostics.ShouldContain(static diagnostic => diagnostic.Id == "CSLDG008");
        invalid.GeneratedSources.Keys.ShouldBe(["LdgSQLiteGeneratorAttributes.g.cs"]);
        AssertSameExtractedModels(valid, invalid);

        GeneratorRunResult restored = GeneratorTestHost.ReRun(invalid,
            new Dictionary<int, string> { [0] = EnvironmentSource });
        AssertClean(valid);
        AssertClean(restored);
        AssertSameSources(valid, restored);
    }

    private static readonly string[] EffectiveEmissionTrackingNames =
    [
        LightSQLiteGenerator.RuntimeEmissionTrackingName,
        LightSQLiteGenerator.TableEmissionTrackingName,
        LightSQLiteGenerator.FtsEmissionTrackingName,
    ];

    private const string JsonFactorySource = """
        public static class JsonFactory
        {
            public static System.Text.Json.JsonSerializerOptions First() => new() { WriteIndented = false };
            public static System.Text.Json.JsonSerializerOptions Second() => new();
        }
        """;

    private const string JsonModelsSource = """
        using CsLightDbGen.SQLiteGenerator;
        namespace Demo;
        public class Payload { public string Text { get; set; } = ""; }
        [LdgSQLiteTable("first"), LdgSQLiteJsonOptions(typeof(JsonFactory), "First")]
        public partial class First { public Payload Value { get; set; } = new(); }
        [LdgSQLiteTable("second"), LdgSQLiteJsonOptions(typeof(JsonFactory), "Second")]
        public partial class Second { public Payload Value { get; set; } = new(); }
        [LdgSQLiteFtsTable("second"), LdgSQLiteJsonOptions(typeof(JsonFactory), "Second")]
        public partial class Search { public Payload Value { get; set; } = new(); }
        """;

    [Fact]
    public void JsonBinding_UnrelatedAndFactoryBodyEditsReuseOutputs()
    {
        GeneratorRunResult first = GeneratorTestHost.Run(
            new GeneratorTestOptions { TrackIncrementalSteps = true }, JsonModelsSource, JsonFactorySource, UnrelatedSource);
        GeneratorRunResult unrelated = GeneratorTestHost.ReRun(first,
            new Dictionary<int, string> { [2] = UnrelatedSource.Replace("int X", "long X", StringComparison.Ordinal) });
        GeneratorRunResult factoryEdit = GeneratorTestHost.ReRun(unrelated,
            new Dictionary<int, string> { [1] = JsonFactorySource.Replace("WriteIndented = false", "WriteIndented = true", StringComparison.Ordinal) });
        AssertClean(first);
        foreach (GeneratorRunResult current in new[] { unrelated, factoryEdit })
        {
            AssertClean(current);
            AssertSameSources(first, current);
            foreach (string name in EffectiveEmissionTrackingNames)
            {
                AssertAllStepOutputsReused(current.DriverRunResult, name);
                AssertSourceOutputReasons(current.DriverRunResult, name, changed: false);
            }
        }
    }

    [Fact]
    public void JsonBinding_ModelSelectionChangeInvalidatesOnlyThatModel()
    {
        int split = JsonModelsSource.IndexOf("[LdgSQLiteTable(\"second\")", StringComparison.Ordinal);
        string selectedModel = JsonModelsSource.Substring(0, split);
        string otherModels = "using CsLightDbGen.SQLiteGenerator; namespace Demo; " + JsonModelsSource.Substring(split);
        GeneratorRunResult first = GeneratorTestHost.Run(
            new GeneratorTestOptions { TrackIncrementalSteps = true }, selectedModel, otherModels, JsonFactorySource);
        GeneratorRunResult changed = GeneratorTestHost.ReRun(first,
            new Dictionary<int, string> { [0] = selectedModel.Replace("typeof(JsonFactory), \"First\"", "typeof(JsonFactory), \"Second\"", StringComparison.Ordinal) });
        AssertClean(first);
        AssertClean(changed);
        foreach (KeyValuePair<string, string> source in first.GeneratedSources)
        {
            if (source.Key.EndsWith("First.Table.g.cs", StringComparison.Ordinal))
            {
                changed.GeneratedSources[source.Key].ShouldNotBe(source.Value);
            }
            else
            {
                changed.GeneratedSources[source.Key].ShouldBe(source.Value);
            }
        }
        IncrementalStepRunReason[] tableReasons = changed.DriverRunResult.Results.Single().TrackedSteps[LightSQLiteGenerator.TableEmissionTrackingName]
            .SelectMany(static step => step.Outputs).Select(static output => output.Reason).ToArray();
        tableReasons.Count(static reason => reason == IncrementalStepRunReason.Modified).ShouldBe(1);
        tableReasons.Count(static reason => reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged).ShouldBe(1);
        AssertAllStepOutputsReused(changed.DriverRunResult, LightSQLiteGenerator.FtsEmissionTrackingName);
        AssertSourceOutputReasons(changed.DriverRunResult, LightSQLiteGenerator.FtsEmissionTrackingName, changed: false);
        IncrementalStepRunReason[] outputReasons = changed.DriverRunResult.Results.Single().TrackedOutputSteps.Values
            .SelectMany(static steps => steps)
            .Where(static step => step.Inputs.Any(static input => input.Source.Name == LightSQLiteGenerator.TableEmissionTrackingName))
            .SelectMany(static step => step.Outputs).Select(static output => output.Reason).ToArray();
        outputReasons.Count(static reason => reason == IncrementalStepRunReason.Modified).ShouldBe(1);
        outputReasons.Count(static reason => reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged).ShouldBe(1);
    }

    [Fact]
    public void JsonBinding_FtsSelectionChangeReusesTableOutputs()
    {
        GeneratorRunResult first = GeneratorTestHost.Run(
            new GeneratorTestOptions { TrackIncrementalSteps = true }, JsonModelsSource, JsonFactorySource);
        GeneratorRunResult changed = GeneratorTestHost.ReRun(first, new Dictionary<int, string>
        {
            [0] = JsonModelsSource.Replace(
                "LdgSQLiteFtsTable(\"second\"), LdgSQLiteJsonOptions(typeof(JsonFactory), \"Second\")",
                "LdgSQLiteFtsTable(\"second\"), LdgSQLiteJsonOptions(typeof(JsonFactory), \"First\")",
                StringComparison.Ordinal),
        });
        AssertClean(first);
        AssertClean(changed);
        AssertAllStepOutputsReused(changed.DriverRunResult, LightSQLiteGenerator.TableEmissionTrackingName);
        AssertSourceOutputReasons(changed.DriverRunResult, LightSQLiteGenerator.TableEmissionTrackingName, changed: false);
        AssertStepChanged(changed.DriverRunResult, LightSQLiteGenerator.FtsEmissionTrackingName);
        AssertSourceOutputReasons(changed.DriverRunResult, LightSQLiteGenerator.FtsEmissionTrackingName, changed: true);
    }

    private static void AssertEnvironmentChange(GeneratorRunResult previous, GeneratorRunResult current, string runtimeNamespace)
    {
        AssertClean(previous);
        AssertClean(current);
        AssertSameExtractedModels(previous, current);
        AssertStepChanged(current.DriverRunResult, LightSQLiteGenerator.EnvironmentTrackingName);
        foreach (string name in EffectiveEmissionTrackingNames)
        {
            AssertStepChanged(current.DriverRunResult, name);
            AssertSourceOutputReasons(current.DriverRunResult, name, changed: true);
        }

        current.GeneratedSources.Keys.OrderBy(static name => name, StringComparer.Ordinal)
            .ShouldBe(previous.GeneratedSources.Keys.OrderBy(static name => name, StringComparer.Ordinal));
        current.GeneratedSources["LdgSQLiteGeneratorAttributes.g.cs"]
            .ShouldBe(previous.GeneratedSources["LdgSQLiteGeneratorAttributes.g.cs"]);
        foreach (string name in current.GeneratedSources.Keys.Where(static name => name != "LdgSQLiteGeneratorAttributes.g.cs"))
        {
            string source = current.GeneratedSources[name];
            source.ShouldNotBe(previous.GeneratedSources[name]);
            source.ShouldContain(name == "LdgSQLiteRuntimeHelpers.g.cs"
                ? "namespace " + runtimeNamespace
                : "new global::" + runtimeNamespace + ".LdgCommandFailedException");
        }
    }

    private static void AssertSameSources(GeneratorRunResult expected, GeneratorRunResult actual)
    {
        actual.GeneratedSources.Count.ShouldBe(expected.GeneratedSources.Count);
        foreach (KeyValuePair<string, string> source in expected.GeneratedSources)
        {
            actual.GeneratedSources[source.Key].ShouldBe(source.Value);
        }
    }

    private static void AssertClean(GeneratorRunResult run)
    {
        run.DriverRunResult.Diagnostics.ShouldBeEmpty();
        run.OutputDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    private static void AssertStepChanged(GeneratorDriverRunResult result, string trackingName)
    {
        IncrementalStepRunReason[] reasons = result.Results.Single().TrackedSteps[trackingName]
            .SelectMany(static step => step.Outputs).Select(static output => output.Reason).ToArray();
        reasons.ShouldNotBeEmpty();
        reasons.ShouldContain(static reason => reason == IncrementalStepRunReason.Modified || reason == IncrementalStepRunReason.New);
        reasons.ShouldAllBe(static reason => reason == IncrementalStepRunReason.Modified ||
            reason == IncrementalStepRunReason.New || reason == IncrementalStepRunReason.Removed);
    }

    private static void AssertSameExtractedModels(GeneratorRunResult previous, GeneratorRunResult current)
    {
        // Changing parse options reparses every tree. Roslyn may remove/re-add syntax-provider
        // entries, but the extracted equatable models must still describe the same tables/FTS.
        string[] modelTrackingNames = [LightSQLiteGenerator.TableModelTrackingName, LightSQLiteGenerator.FtsModelTrackingName];
        foreach (string name in modelTrackingNames)
        {
            object[] expected = previous.DriverRunResult.Results.Single().TrackedSteps[name]
                .SelectMany(static step => step.Outputs)
                .Where(static output => output.Reason != IncrementalStepRunReason.Removed)
                .Select(static output => output.Value).ToArray();
            object[] actual = current.DriverRunResult.Results.Single().TrackedSteps[name]
                .SelectMany(static step => step.Outputs)
                .Where(static output => output.Reason != IncrementalStepRunReason.Removed)
                .Select(static output => output.Value).ToArray();
            actual.ShouldBe(expected);
        }
    }

    private static void AssertSourceOutputReasons(GeneratorDriverRunResult result, string inputTrackingName, bool changed)
    {
        // Inspect the actual source-output steps, not just extraction or equal source strings.
        IncrementalStepRunReason[] reasons = result.Results.Single().TrackedOutputSteps.Values
            .SelectMany(static steps => steps)
            .Where(step => step.Inputs.Any(input => input.Source.Name == inputTrackingName))
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();
        reasons.ShouldNotBeEmpty($"expected a source output consuming '{inputTrackingName}'");
        if (changed)
        {
            reasons.ShouldContain(static reason => reason == IncrementalStepRunReason.Modified || reason == IncrementalStepRunReason.New);
        }
        reasons.ShouldAllBe(reason => changed
            ? reason == IncrementalStepRunReason.Modified || reason == IncrementalStepRunReason.New || reason == IncrementalStepRunReason.Removed
            : reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }

    private static void AssertAllStepOutputsReused(GeneratorDriverRunResult runResult, string trackingName)
    {
        runResult.Results.Length.ShouldBe(1);

        IReadOnlyDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> trackedSteps =
            runResult.Results[0].TrackedSteps;

        trackedSteps.ContainsKey(trackingName).ShouldBeTrue($"expected a tracked incremental step named '{trackingName}'");

        IReadOnlyList<IncrementalStepRunReason> reasons = trackedSteps[trackingName]
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();

        reasons.ShouldNotBeEmpty();
        reasons.ShouldAllBe(reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);
    }
}
