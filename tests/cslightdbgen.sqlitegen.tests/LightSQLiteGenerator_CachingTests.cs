using System.Collections.Immutable;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using CsLightDbGen.SQLiteGenerator;
using Microsoft.CodeAnalysis;
using Shouldly;

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
