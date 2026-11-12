using System.Collections.Immutable;
using CsLightDbGen.SQLiteGenerator;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using GeneratorRunResult = cslightdbgen.sqlitegen.tests.TestInfrastructure.GeneratorRunResult;

namespace cslightdbgen.sqlitegen.tests;

// Default hosts keep public annotations and the historical single-analyzer-host topology.
// Explicit local hosts instead own internal annotations and independently named public helpers.
public class LightSQLiteGenerator_CrossProjectTests
{
    private const string HostSource = """
        using CsLightDbGen.SQLiteGenerator;

        namespace Data;

        [LdgSQLiteTable("customers")]
        public partial class Customer
        {
            [LdgSQLiteKey]
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }
        """;

    // Consumer references the host as metadata only (no analyzer) and binds:
    //   * typeof(LdgSQLiteTable) — proves the attribute type is PUBLIC across the assembly boundary
    //     (an internal attribute would not bind here),
    //   * Customer.CreateTable(IDbConnection) and Customer.SQLiteColumnNames — the generated public
    //     static DAL surface produced in the host project.
    private const string ConsumerSource = """
        using System;
        using System.Collections.Generic;
        using System.Data;
        using CsLightDbGen.SQLiteGenerator;
        using Data;

        namespace ConsumerApp;

        public static class CustomerUsage
        {
            public static Type TableAttributeType => typeof(LdgSQLiteTable);

            public static bool Create(IDbConnection connection) => Customer.CreateTable(connection);

            public static IReadOnlyCollection<string> Columns => Customer.SQLiteColumnNames;
        }
        """;

    // A second model, in the "main" project, that also carries the attribute. Used only by the
    // both-projects-host-the-analyzer scenario below.
    private const string SecondModelSource = """
        using CsLightDbGen.SQLiteGenerator;

        namespace Data2;

        [LdgSQLiteTable("orders")]
        public partial class Order
        {
            [LdgSQLiteKey]
            public int Id { get; set; }
        }
        """;

    [Fact]
    public void TwoProjectAnalyzerUse_HasNoAttributeTypeConflict()
    {
        (CSharpCompilation analyzerHost, CSharpCompilation consumer) =
            GeneratorTestHost.CompileConsumerReferencingGeneratedProject(HostSource, ConsumerSource);

        // The single project that runs the generator compiles cleanly: public attributes + DAL.
        analyzerHost.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();

        ImmutableArray<Diagnostic> consumerDiagnostics = consumer.GetDiagnostics();

        // The whole point of D2: consuming the generated project from a second project yields NO
        // duplicate attribute-type conflict, because only the host runs the analyzer.
        consumerDiagnostics.ShouldNotContain(d => d.Id == "CS0436");

        // The consumer binds the host's public attribute type and generated public static APIs with
        // no compilation errors at all.
        consumerDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();
    }

    [Fact]
    public void BothProjectsHostAnalyzer_ProducesCs0436AsWarningNotError()
    {
        // If BOTH projects run the analyzer, the downstream project acquires the attribute types
        // twice: once source-generated locally and once imported from the referenced (also-generated)
        // assembly. Default mode intentionally retains that warning; independently owned local
        // hosts below avoid it without warning suppression.
        TestInfrastructure.GeneratorRunResult result = GeneratorTestHost.RunWithReference(HostSource, SecondModelSource);

        ImmutableArray<Diagnostic> diagnostics = result.OutputCompilation.GetDiagnostics();

        diagnostics.ShouldContain(d => d.Id == "CS0436");
        diagnostics
            .Where(d => d.Id == "CS0436")
            .ShouldAllBe(d => d.Severity == DiagnosticSeverity.Warning);
        result.Errors.ShouldBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();
    }

    private static readonly CSharpParseOptions LocalParseOptions =
        CSharpParseOptions.Default.WithPreprocessorSymbols("CSLIGHTDBGEN_MULTI_HOST");

    private const string LocalHostA = """
        using CsLightDbGen.SQLiteGenerator;
        [assembly: LdgSQLiteRuntimeNamespace("Example.HostA.Runtime")]

        namespace HostA;

        [LdgSQLiteBaseClass]
        public class SharedBase
        {
            public string Label { get; set; } = string.Empty;
        }

        [LdgSQLiteTable("a_items")]
        public partial class Item : SharedBase
        {
            [LdgSQLiteKey]
            public int Id { get; set; }
        }
        """;

    private const string LocalHostB = """
        using CsLightDbGen.SQLiteGenerator;
        [assembly: LdgSQLiteRuntimeNamespace("Example.HostB.Runtime")]

        namespace HostB;

        [LdgSQLiteTable("b_items")]
        public partial class Item : HostA.SharedBase
        {
            [LdgSQLiteKey]
            public int Id { get; set; }

            [LdgSQLiteForeignKey("a_items", "Id", onDelete: LdgSQLiteFkAction.Cascade)]
            public int ParentId { get; set; }
        }

        [LdgSQLiteFtsTable("a_items")]
        public partial class Search
        {
            public string Label { get; set; } = string.Empty;
        }
        """;

    [Fact]
    public void LocalHosts_EmittedImages_CompileThirdConsumerWithoutConflicts()
    {
        (GeneratorRunResult hostA, EmittedReferenceResult imageA, GeneratorRunResult hostB, EmittedReferenceResult imageB) =
            CompileLocalHosts();

        CSharpCompilation consumer = GeneratorTestHost.CreateCompilation(
            new GeneratorTestOptions
            {
                AssemblyName = "LocalConsumerC",
                AdditionalReferences = [imageA.Reference, imageB.Reference],
            },
            """
            using System.Data;
            using HostA;
            using HostB;

            public static class Consumer
            {
                public static bool Create(IDbConnection connection) =>
                    HostA.Item.CreateTable(connection) && HostB.Item.CreateTable(connection);

                public static int Populate(IDbConnection connection) => Search.Populate(connection);

                public static bool CreateWithExtensions(IDbConnection connection) =>
                    HostA.ItemExtensions.EnsureSchema<HostA.Item>(connection) &&
                    HostB.ItemExtensions.EnsureSchema<HostB.Item>(connection);

                public static System.Exception ExceptionA() =>
                    new Example.HostA.Runtime.LdgCommandFailedException("A", "sql-a");
                public static System.Exception ExceptionB() =>
                    new Example.HostB.Runtime.LdgCommandFailedException("B", "sql-b");

                public static string CatchBoth(System.Action operation)
                {
                    try { operation(); }
                    catch (Example.HostA.Runtime.LdgCommandFailedException error) { return error.ModelName + error.Sql; }
                    catch (Example.HostB.Runtime.LdgCommandFailedException error) { return error.ModelName + error.Sql; }
                    return string.Empty;
                }
            }
            """);

        AssertCleanCompilation(consumer);
        EmittedReferenceResult imageC = GeneratorTestHost.EmitToReference(consumer);
        AssertNoConflictsOrErrors(imageC.Diagnostics);
        hostA.DriverRunResult.Diagnostics.ShouldBeEmpty();
        hostB.DriverRunResult.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void LocalHosts_AnnotationsAndFkEnumAreInternal_ExceptionsRemainPublic()
    {
        (GeneratorRunResult hostA, EmittedReferenceResult imageA, GeneratorRunResult hostB, EmittedReferenceResult imageB) =
            CompileLocalHosts();
        string[] annotationNames =
        [
            "LdgSQLiteRuntimeNamespace", "LdgSQLiteBaseClass", "LdgSQLiteTable", "LdgSQLiteIndex",
            "LdgSQLiteKey", "LdgSQLiteForeignKey", "LdgSQLiteForeignKeyComposite", "LdgSQLiteIgnore",
            "LdgSQLiteUnique", "LdgSQLiteMultiSelect", "LdgSQLiteDefault", "LdgSQLiteFtsTable",
            "LdgSQLiteFtsUnindexed",
        ];

        GeneratorRunResult[] hosts = [hostA, hostB];
        foreach (GeneratorRunResult host in hosts)
        {
            foreach (string name in annotationNames)
            {
                INamedTypeSymbol? annotation = host.OutputCompilation.Assembly.GetTypeByMetadataName(
                    "CsLightDbGen.SQLiteGenerator." + name);
                annotation.ShouldNotBeNull();
                annotation.DeclaredAccessibility.ShouldBe(Accessibility.Internal);
                annotation.InstanceConstructors.ShouldAllBe(static ctor => ctor.DeclaredAccessibility == Accessibility.Public);
                annotation.GetMembers().OfType<IPropertySymbol>()
                    .ShouldAllBe(static property => property.DeclaredAccessibility == Accessibility.Public);
            }

            INamedTypeSymbol? fkEnum = host.OutputCompilation.Assembly.GetTypeByMetadataName(
                "CsLightDbGen.SQLiteGenerator.LdgSQLiteFkAction");
            fkEnum.ShouldNotBeNull();
            fkEnum.DeclaredAccessibility.ShouldBe(Accessibility.Internal);
            fkEnum.TypeKind.ShouldBe(TypeKind.Enum);
            host.OutputCompilation.Assembly.GetTypeByMetadataName(
                "CsLightDbGen.SQLiteGenerator.LdgCommandFailedException").ShouldBeNull();
            host.OutputCompilation.Assembly.GetAttributes()
                .ShouldNotContain(static attribute => attribute.AttributeClass != null && attribute.AttributeClass.Name == "InternalsVisibleToAttribute");
        }

        AssertPublicException(hostA, "Example.HostA.Runtime");
        AssertPublicException(hostB, "Example.HostB.Runtime");

        string inaccessibleNames = string.Join(",",
            annotationNames.Append("LdgSQLiteFkAction")
                .Select(static name => "typeof(CsLightDbGen.SQLiteGenerator." + name + ")"));
        CSharpCompilation consumer = GeneratorTestHost.CreateCompilation(
            new GeneratorTestOptions
            {
                AssemblyName = "AnnotationAccessConsumer",
                AdditionalReferences = [imageA.Reference, imageB.Reference],
            },
            "public class Access { public System.Type[] Types => new System.Type[] { " + inaccessibleNames + " }; }");
        Diagnostic[] accessErrors = consumer.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        accessErrors.Length.ShouldBe(annotationNames.Length + 1);
        accessErrors.ShouldAllBe(static diagnostic => diagnostic.Id == "CS0122");
        consumer.GetDiagnostics().ShouldNotContain(static diagnostic => diagnostic.Id == "CS0433" || diagnostic.Id == "CS0436");
    }

    [Fact]
    public void LocalHosts_ReferencedBaseAndFtsMetadataRemainDiscoverable()
    {
        (GeneratorRunResult hostA, EmittedReferenceResult imageA, GeneratorRunResult hostB, EmittedReferenceResult imageB) =
            CompileLocalHosts();
        string table = GeneratorTestHost.GetGeneratedSourceByHintSuffix(hostB, "Item.Table.g.cs");
        string fts = GeneratorTestHost.GetGeneratedSourceByHintSuffix(hostB, "Search.Fts.g.cs");
        table.ShouldContain("\"Label\"");
        table.ShouldContain("value.Label");
        table.ShouldContain("REFERENCES \"a_items\"(\"Id\")");
        table.ShouldContain("ON DELETE CASCADE");
        fts.ShouldContain("a_items");
        fts.ShouldContain("\"Label\"");
        fts.ShouldContain("new global::Example.HostB.Runtime.LdgCommandFailedException");
        table.ShouldContain("new global::Example.HostB.Runtime.LdgCommandFailedException");
        foreach (string source in hostB.GeneratedSources.Where(static pair => pair.Key != "LdgSQLiteGeneratorAttributes.g.cs").Select(static pair => pair.Value))
        {
            source.ShouldNotContain("global::CsLightDbGen.SQLiteGenerator.LdgCommandFailedException");
            source.ShouldNotContain("global::Example.HostA.Runtime.LdgCommandFailedException");
        }
        hostB.DriverRunResult.Diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == "CSLDG006");
    }

    [Fact]
    public void LocalHost_NoModelsStillOwnsPublicHelper()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(LocalOptions("EmptyLocalHost"),
            """[assembly: CsLightDbGen.SQLiteGenerator.LdgSQLiteRuntimeNamespace("Empty.Host.Runtime")]""");
        AssertCleanHost(run);
        run.GeneratedSources.Keys.OrderBy(static name => name, StringComparer.Ordinal).ShouldBe(
            ["LdgSQLiteGeneratorAttributes.g.cs", "LdgSQLiteRuntimeHelpers.g.cs"]);
        AssertPublicException(run, "Empty.Host.Runtime");
        AssertNoConflictsOrErrors(GeneratorTestHost.EmitToReference(run.OutputCompilation).Diagnostics);
    }

    [Fact]
    public void LocalHost_PartialFtsAnnotations_EmitOnce()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(LocalOptions("PartialFtsHost"),
            """
            using CsLightDbGen.SQLiteGenerator;
            [assembly: LdgSQLiteRuntimeNamespace("Partial.Host.Runtime")]
            namespace PartialHost;
            [LdgSQLiteTable("items")]
            public partial class Item
            {
                [LdgSQLiteKey] public int Id { get; set; }
                public string Text { get; set; } = string.Empty;
            }
            [LdgSQLiteFtsTable("items")]
            public partial class Search { }
            """,
            """
            using CsLightDbGen.SQLiteGenerator;
            namespace PartialHost;
            [LdgSQLiteBaseClass]
            public partial class Search
            {
                public string Text { get; set; } = string.Empty;
            }
            """);
        AssertCleanHost(run);
        run.GeneratedSources.Keys.Count(static name => name.EndsWith("Search.Fts.g.cs", StringComparison.Ordinal)).ShouldBe(1);
        GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "Search.Fts.g.cs").ShouldContain("\"Text\"");
        AssertNoConflictsOrErrors(GeneratorTestHost.EmitToReference(run.OutputCompilation).Diagnostics);
    }

    [Theory]
    [InlineData(true, null, "Add [assembly:")]
    [InlineData(false, "Example.Local.Runtime", "project-wide CSLIGHTDBGEN_MULTI_HOST")]
    [InlineData(true, null, "ordinary C# identifiers", true)]
    [InlineData(true, "", "ordinary C# identifiers")]
    [InlineData(true, " ", "ordinary C# identifiers")]
    [InlineData(true, "Example..Runtime", "ordinary C# identifiers")]
    [InlineData(true, ".Example", "ordinary C# identifiers")]
    [InlineData(true, "Example.", "ordinary C# identifiers")]
    [InlineData(true, "Example.class", "ordinary C# identifiers")]
    [InlineData(true, "global::Example.Runtime", "ordinary C# identifiers")]
    [InlineData(true, "Example.@Runtime", "ordinary C# identifiers")]
    [InlineData(true, "Example.\\u0052untime", "ordinary C# identifiers")]
    [InlineData(true, "Example.Runtime; class Injected {}", "ordinary C# identifiers")]
    [InlineData(true, "Example.123", "ordinary C# identifiers")]
    [InlineData(true, "CsLightDbGen.SQLiteGenerator", "host-specific")]
    public void MultiHost_InvalidConfiguration_ReportsCSLDG008(
        bool multiHost, string? runtimeNamespace, string reason, bool explicitNull = false)
    {
        string annotation = runtimeNamespace is null && !explicitNull
            ? string.Empty
            : "[assembly: CsLightDbGen.SQLiteGenerator.LdgSQLiteRuntimeNamespace(" +
                (explicitNull ? "null" : Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(runtimeNamespace ?? string.Empty, true)) + ")]";
        GeneratorRunResult run = GeneratorTestHost.Run(
            new GeneratorTestOptions
            {
                AssemblyName = "InvalidHost",
                ParseOptions = multiHost ? LocalParseOptions : CSharpParseOptions.Default,
            },
            annotation, HostSource,
            """
            using CsLightDbGen.SQLiteGenerator;
            [LdgSQLiteFtsTable("unknown")]
            public partial class RejectedSearch { public string Text { get; set; } = string.Empty; }
            """);
        AssertRejectedHost(run, 1, annotation.Length == 0 ? null : annotation, reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MultiHost_RuntimeNameCollision_ReportsEachAssemblyAndSuppressesOrdinaryOutput(int referenceCount)
    {
        const string collision = "namespace Collision.Runtime { public class LdgCommandFailedException : System.Exception { } }";
        const string annotation = """[assembly: CsLightDbGen.SQLiteGenerator.LdgSQLiteRuntimeNamespace("Collision.Runtime")]""";
        List<MetadataReference> references = [];
        for (int index = 0; index < referenceCount; index++)
        {
            CSharpCompilation referenced = GeneratorTestHost.CreateCompilation(
                new GeneratorTestOptions { AssemblyName = "CollisionImage" + index }, collision);
            AssertCleanCompilation(referenced);
            EmittedReferenceResult image = GeneratorTestHost.EmitToReference(referenced);
            AssertNoConflictsOrErrors(image.Diagnostics);
            references.Add(image.Reference);
        }

        GeneratorRunResult run = GeneratorTestHost.Run(
            LocalOptions("CollidingHost", references),
            annotation, HostSource, referenceCount == 0 ? collision : "public class Bystander { }");
        AssertRejectedHost(run, Math.Max(1, referenceCount), annotation,
            referenceCount == 0 ? "this assembly" : "referenced assembly");
        if (referenceCount == 2)
        {
            run.InputCompilation.GetTypeByMetadataName("Collision.Runtime.LdgCommandFailedException").ShouldBeNull();
        }
        for (int index = 0; index < referenceCount; index++)
        {
            run.DriverRunResult.Diagnostics.ShouldContain(diagnostic =>
                diagnostic.GetMessage().Contains("CollisionImage" + index, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void MultiHost_SourceFileDefineIsNotProjectConfiguration()
    {
        const string source = """
            #define CSLIGHTDBGEN_MULTI_HOST
            [assembly: CsLightDbGen.SQLiteGenerator.LdgSQLiteRuntimeNamespace("File.Local.Runtime")]
            """;
        GeneratorRunResult run = GeneratorTestHost.Run(source, HostSource);
        AssertRejectedHost(run, 1, source, "project-wide CSLIGHTDBGEN_MULTI_HOST");
    }

    [Theory]
    [InlineData("Example.record.Runtime")]
    [InlineData("Example.Δοκιμή.Runtime_1")]
    public void MultiHost_OrdinaryIdentifierNamespaces_Compile(string runtimeNamespace)
    {
        GeneratorRunResult run = GeneratorTestHost.Run(LocalOptions("OrdinaryNamespaceHost"),
            "[assembly: CsLightDbGen.SQLiteGenerator.LdgSQLiteRuntimeNamespace(" +
            Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(runtimeNamespace, true) + ")]",
            HostSource);
        AssertCleanHost(run);
        AssertPublicException(run, runtimeNamespace);
        AssertNoConflictsOrErrors(GeneratorTestHost.EmitToReference(run.OutputCompilation).Diagnostics);
    }

    private static GeneratorTestOptions LocalOptions(string assemblyName, IReadOnlyList<MetadataReference>? references = null) =>
        new GeneratorTestOptions
        {
            AssemblyName = assemblyName,
            ParseOptions = LocalParseOptions,
            AdditionalReferences = references ?? [],
        };

    private static (GeneratorRunResult HostA, EmittedReferenceResult ImageA, GeneratorRunResult HostB, EmittedReferenceResult ImageB)
        CompileLocalHosts()
    {
        GeneratorRunResult hostA = GeneratorTestHost.Run(LocalOptions("LocalHostA"), LocalHostA);
        AssertCleanHost(hostA);
        EmittedReferenceResult imageA = GeneratorTestHost.EmitToReference(hostA.OutputCompilation);
        AssertNoConflictsOrErrors(imageA.Diagnostics);
        GeneratorRunResult hostB = GeneratorTestHost.Run(LocalOptions("LocalHostB", [imageA.Reference]), LocalHostB);
        AssertCleanHost(hostB);
        EmittedReferenceResult imageB = GeneratorTestHost.EmitToReference(hostB.OutputCompilation);
        AssertNoConflictsOrErrors(imageB.Diagnostics);
        return (hostA, imageA, hostB, imageB);
    }

    private static void AssertPublicException(GeneratorRunResult host, string runtimeNamespace)
    {
        INamedTypeSymbol? exception = host.OutputCompilation.Assembly.GetTypeByMetadataName(
            runtimeNamespace + ".LdgCommandFailedException");
        exception.ShouldNotBeNull();
        exception.DeclaredAccessibility.ShouldBe(Accessibility.Public);
        exception.IsSealed.ShouldBeTrue();
        exception.BaseType?.ToDisplayString().ShouldBe("System.Exception");
        exception.InstanceConstructors.Single().DeclaredAccessibility.ShouldBe(Accessibility.Public);
        exception.GetMembers().OfType<IPropertySymbol>().Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal).ShouldBe(["ModelName", "Sql"]);
        host.GeneratedSources["LdgSQLiteRuntimeHelpers.g.cs"].ShouldContain(
            "The command for model '{modelName}' affected no rows (expected at least one). SQL: {sql}");
    }

    private static void AssertRejectedHost(GeneratorRunResult run, int diagnosticCount, string? annotationSource, string reason)
    {
        Diagnostic[] diagnostics = run.DriverRunResult.Diagnostics.ToArray();
        diagnostics.Length.ShouldBe(diagnosticCount);
        diagnostics.ShouldAllBe(static diagnostic => diagnostic.Id == "CSLDG008" && diagnostic.Severity == DiagnosticSeverity.Error);
        foreach (Diagnostic diagnostic in diagnostics)
        {
            diagnostic.GetMessage().ShouldContain(reason);
            if (annotationSource is null)
            {
                diagnostic.Location.ShouldBe(Location.None);
            }
            else
            {
                diagnostic.Location.Kind.ShouldBe(LocationKind.ExternalFile);
                SyntaxTree tree = run.InputCompilation.SyntaxTrees.First();
                AttributeSyntax annotation = tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>()
                    .Single(static attribute => attribute.Name.ToString().EndsWith("LdgSQLiteRuntimeNamespace", StringComparison.Ordinal));
                diagnostic.Location.SourceSpan.ShouldBe(annotation.Span);
                diagnostic.Location.GetLineSpan().Span.ShouldBe(annotation.GetLocation().GetLineSpan().Span);
                diagnostic.Location.GetLineSpan().Path.ShouldBe(tree.FilePath);
                string annotationText = annotationSource.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
                annotationText.ShouldStartWith("CsLightDbGen.SQLiteGenerator.LdgSQLiteRuntimeNamespace(");
                annotationText.ShouldEndWith(")");
            }
        }
        run.Diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == "CS8785" || diagnostic.Id == "CSLDG006");
        run.GeneratedSources.Keys.ShouldBe(["LdgSQLiteGeneratorAttributes.g.cs"]);
        run.CompilationErrors.ShouldBeEmpty();
    }

    private static void AssertCleanHost(GeneratorRunResult run)
    {
        AssertNoConflictsOrErrors(run.DriverRunResult.Diagnostics);
        AssertNoConflictsOrErrors(run.OutputDiagnostics);
        AssertCleanCompilation(run.OutputCompilation);
    }

    private static void AssertCleanCompilation(CSharpCompilation compilation) =>
        AssertNoConflictsOrErrors(compilation.GetDiagnostics());

    private static void AssertNoConflictsOrErrors(IEnumerable<Diagnostic> diagnostics)
    {
        diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error ||
            diagnostic.Id == "CS0436" || diagnostic.Id == "CS0433").ShouldBeEmpty();
    }
}
