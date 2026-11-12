using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using GeneratorRunResult = cslightdbgen.sqlitegen.tests.TestInfrastructure.GeneratorRunResult;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_KeyNoUniqueTests
{
    [Theory]
    [InlineData("int", "INTEGER")]
    [InlineData("long", "INTEGER")]
    [InlineData("string", "TEXT")]
    public void NewKey_SingleAndCompositeDdl_RespectsStyle(string type, string sqlType)
    {
        string property = $"public {type} Id {{ get; set; }} = default!;";
        string single = Generate($"[LdgSQLiteKeyNoUnique] {property}");
        single.ShouldContain($"\"Id\" {sqlType} PRIMARY KEY,");
        single.ShouldNotContain($"\"Id\" {sqlType} PRIMARY KEY NOT NULL");
        single.ShouldNotContain($"\"Id\" {sqlType} UNIQUE");

        string composite = Generate($$"""
            [LdgSQLiteKeyNoUnique] {{property}}
            [LdgSQLiteKeyNoUnique] public string Other { get; set; } = string.Empty;
            """);
        composite.ShouldContain($"\"Id\" {sqlType},");
        composite.ShouldContain("\"Other\" TEXT,");
        AssertComposite(composite, "Id", "Other");

        string old = Generate($"[LdgSQLiteKey] {property}");
        old.ShouldContain($"\"Id\" {sqlType} UNIQUE PRIMARY KEY NOT NULL,");
    }

    [Theory]
    [InlineData("int", "")]
    [InlineData("long", "")]
    [InlineData("string", "")]
    [InlineData("int", "(false)")]
    [InlineData("long", "(AutoIncrement = false)")]
    [InlineData("int", "(autoIncrement: false)")]
    [InlineData("long", "(false, AutoIncrement = true)")]
    [InlineData("int", "(true, AutoIncrement = false)")]
    public void NewKey_AutoIncrementAndLogicalKeyConsumers_MatchDefault(string type, string arguments)
    {
        string properties = $"public {type} Id {{ get; set; }} = default!;";
        string old = Generate($"[LdgSQLiteKey{arguments}] {properties}");
        string current = Generate($"[LdgSQLiteKeyNoUnique{arguments}] {properties}");
        current.ShouldBe(old.Replace("UNIQUE PRIMARY KEY NOT NULL", "PRIMARY KEY"));
        current.ShouldContain("SelectDict");
        current.ShouldContain($"IEnumerable<{type}>? IdValues");
        current.ShouldNotContain("ADD COLUMN \"Id\"");
        bool identity = type != "string" && !arguments.EndsWith("false)");
        if (identity)
        {
            current.ShouldContain("GetIndex(");
            current.ShouldContain("RETURNING \"Id\"");
        }
        else
        {
            current.ShouldNotContain("GetIndex(");
            current.ShouldContain("conflictColumns = new string[] { \"Id\" };");
            current.ShouldNotContain("Convert.ToInt32(commandResult)");
            current.ShouldNotContain("Convert.ToInt64(commandResult)");
        }
    }

    [Theory]
    [InlineData("K")]
    [InlineData("CsLightDbGen.SQLiteGenerator.LdgSQLiteKeyNoUnique")]
    [InlineData("global::CsLightDbGen.SQLiteGenerator.LdgSQLiteKeyNoUnique")]
    public void NewKey_AliasesAndQualification_PreserveSemanticValues(string name)
    {
        string source = Generate($"[{name}(AutoIncrement = false)] public long Id {{ get; set; }}");
        source.ShouldNotContain("GetIndex(");
        source.ShouldContain("\"Id\" INTEGER PRIMARY KEY,");
        source.ShouldContain("conflictColumns = new string[] { \"Id\" };");
    }

    [Theory]
    [InlineData("LdgSQLiteKey", "LdgSQLiteKeyNoUnique", "\"Id\" INTEGER NOT NULL,", "\"Other\" TEXT,")]
    [InlineData("LdgSQLiteKeyNoUnique", "LdgSQLiteKey", "\"Id\" INTEGER,", "\"Other\" TEXT NOT NULL,")]
    public void MixedKeys_UseAllMembersWithoutIdentity(string first, string second, string firstDdl, string secondDdl)
    {
        string source = Generate($$"""
            [{{first}}] public int Id { get; set; }
            [{{second}}] public string Other { get; set; } = string.Empty;
            """);
        source.ShouldContain(firstDdl);
        source.ShouldContain(secondDdl);
        AssertComposite(source, "Id", "Other");
    }

    [Theory]
    [InlineData("LdgSQLiteKey")]
    [InlineData("LdgSQLiteKeyNoUnique")]
    public void OldNullableComposite_RetainsExistingNullability(string secondStyle)
    {
        string source = Generate($$"""
            [LdgSQLiteKey] public string? Id { get; set; }
            [{{secondStyle}}] public int Other { get; set; }
            """);
        source.ShouldContain("\"Id\" TEXT,");
        source.ShouldNotContain("\"Id\" TEXT NOT NULL");
        source.ShouldContain(secondStyle == "LdgSQLiteKey" ? "\"Other\" INTEGER NOT NULL," : "\"Other\" INTEGER,");
        AssertComposite(source, "Id", "Other");
    }

    [Fact]
    public void BothKeyAnnotations_ReportCSLDG005()
    {
        GeneratorRunResult run = Run("""
            [LdgSQLiteKey, LdgSQLiteKeyNoUnique]
            public int Id { get; set; }
            """);
        Diagnostic diagnostic = run.DriverRunResult.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("CSLDG005");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain("cannot be combined");
        diagnostic.Location.SourceSpan.ShouldBe(run.InputCompilation.SyntaxTrees.Single().GetRoot()
            .DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>().First().Identifier.Span);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedSources.Keys.ShouldNotContain(name => name.EndsWith("Item.Table.g.cs", StringComparison.Ordinal));
        run.DriverRunResult.Diagnostics.ShouldNotContain(d => d.Id == "CS8785");
    }

    [Theory]
    [InlineData("LdgSQLiteKeyNoUnique(true)", true)]
    [InlineData("LdgSQLiteKeyNoUnique(autoIncrement: true)", true)]
    [InlineData("LdgSQLiteKeyNoUnique(AutoIncrement = true)", true)]
    [InlineData("K(true)", true)]
    [InlineData("global::CsLightDbGen.SQLiteGenerator.LdgSQLiteKeyNoUnique(true)", true)]
    [InlineData("LdgSQLiteKeyNoUnique(false)", false)]
    [InlineData("K(AutoIncrement = false)", false)]
    [InlineData("LdgSQLiteKeyNoUnique(false, AutoIncrement = true)", true)]
    [InlineData("LdgSQLiteKeyNoUnique(true, AutoIncrement = false)", false)]
    public void ExplicitCompositeAutoIncrement_ReportsCSLDG003(string attribute, bool conflict)
    {
        GeneratorRunResult run = Run($$"""
            [{{attribute}}] public int Id { get; set; }
            [LdgSQLiteKeyNoUnique] public long Other { get; set; }
            """);
        run.CompilationErrors.ShouldBeEmpty();
        Diagnostic[] diagnostics = run.DriverRunResult.Diagnostics.ToArray();
        if (conflict)
        {
            Diagnostic diagnostic = diagnostics.ShouldHaveSingleItem();
            diagnostic.Id.ShouldBe("CSLDG003");
            diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
            diagnostic.Location.SourceSpan.ShouldBe(run.InputCompilation.SyntaxTrees.Single().GetRoot()
                .DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>().First().Identifier.Span);
        }
        else
        {
            AssertValid(run);
        }
        AssertComposite(GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "Item.Table.g.cs"), "Id", "Other");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("(true)", false)]
    [InlineData("(false)", false)]
    [InlineData("(AutoIncrement = false)", false)]
    [InlineData("(AutoIncrement = true)", true)]
    [InlineData("(false, AutoIncrement = true)", true)]
    [InlineData("(true, AutoIncrement = false)", false)]
    public void NewKey_MetadataComposite_UsesObservableExplicitness(string arguments, bool conflict)
    {
        CSharpParseOptions localOptions = CSharpParseOptions.Default.WithPreprocessorSymbols("CSLIGHTDBGEN_MULTI_HOST");
        GeneratorRunResult host = GeneratorTestHost.Run(
            new GeneratorTestOptions { AssemblyName = "KeyMetadataBase", ParseOptions = localOptions },
            $$"""
            using CsLightDbGen.SQLiteGenerator;
            [assembly: LdgSQLiteRuntimeNamespace("KeyMetadata.Runtime")]
            namespace Referenced;
            [LdgSQLiteBaseClass]
            public class Base
            {
                [LdgSQLiteKeyNoUnique{{arguments}}] public int Id { get; set; }
            }
            """);
        AssertValid(host);
        EmittedReferenceResult image = GeneratorTestHost.EmitToReference(host.OutputCompilation);
        image.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        GeneratorRunResult derived = GeneratorTestHost.Run(
            new GeneratorTestOptions { AssemblyName = "KeyMetadataDerived", AdditionalReferences = [image.Reference] },
            """
            using CsLightDbGen.SQLiteGenerator;
            namespace Derived;
            [LdgSQLiteTable]
            public partial class Item : Referenced.Base
            {
                [LdgSQLiteKeyNoUnique] public long Other { get; set; }
                public string Label { get; set; } = string.Empty;
            }
            """);
        derived.CompilationErrors.ShouldBeEmpty();
        if (conflict)
        {
            derived.DriverRunResult.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("CSLDG003");
        }
        else
        {
            AssertValid(derived);
        }
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(derived, "Item.Table.g.cs");
        AssertComposite(source, "Id", "Other");
        source.ShouldContain("\"Id\" INTEGER,");

        // The same metadata effective false also disables identity on a single inherited key.
        if (arguments.Contains("false", StringComparison.Ordinal) && !conflict)
        {
            GeneratorRunResult single = GeneratorTestHost.Run(
                new GeneratorTestOptions { AssemblyName = "KeyMetadataSingle", AdditionalReferences = [image.Reference] },
                """
                using CsLightDbGen.SQLiteGenerator;
                namespace Derived;
                [LdgSQLiteTable] public partial class Item : Referenced.Base
                {
                    public string Label { get; set; } = string.Empty;
                }
                """);
            AssertValid(single);
            GeneratorTestHost.GetGeneratedSourceByHintSuffix(single, "Item.Table.g.cs").ShouldNotContain("GetIndex(");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewKey_ExplicitUniquenessAndEnsureSchemaExclusion_RemainEffective(bool composite)
    {
        string source = Generate($$"""
            [LdgSQLiteKeyNoUnique, LdgSQLiteUnique, LdgSQLiteDefault("key")]
            public string Id { get; set; } = string.Empty;
            {{(composite ? "[LdgSQLiteKey] public int Other { get; set; }" : string.Empty)}}
            """, """[LdgSQLiteUnique("Id", "Label"), LdgSQLiteIndex("Label", "Id", Unique = true, Where = "Label <> ''")]""");
        source.ShouldContain(composite ? "\"Id\" TEXT UNIQUE DEFAULT 'key'" : "\"Id\" TEXT PRIMARY KEY UNIQUE DEFAULT 'key'");
        source.ShouldContain("UNIQUE (\"Id\", \"Label\")");
        source.ShouldContain("CREATE UNIQUE INDEX IF NOT EXISTS");
        source.ShouldContain("\"Label\", \"Id\"");
        source.ShouldContain(") WHERE Label <> ''");
        source.ShouldNotContain("ADD COLUMN \"Id\"");
        source.ShouldNotContain("ADD COLUMN \"Other\"");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewKey_IsIndependentAndUsesHostAccessibility(bool local)
    {
        GeneratorRunResult run = GeneratorTestHost.Run(
            new GeneratorTestOptions
            {
                ParseOptions = local ? CSharpParseOptions.Default.WithPreprocessorSymbols("CSLIGHTDBGEN_MULTI_HOST") : CSharpParseOptions.Default,
            },
            "using CsLightDbGen.SQLiteGenerator;" +
            (local ? "[assembly: LdgSQLiteRuntimeNamespace(\"Keys.Runtime\")]" : string.Empty));
        AssertValid(run);
        INamedTypeSymbol attribute = run.OutputCompilation.Assembly.GetTypeByMetadataName(
            "CsLightDbGen.SQLiteGenerator.LdgSQLiteKeyNoUnique")!;
        attribute.DeclaredAccessibility.ShouldBe(local ? Accessibility.Internal : Accessibility.Public);
        attribute.BaseType!.ToDisplayString().ShouldBe("System.Attribute");
        attribute.InstanceConstructors.ShouldHaveSingleItem().Parameters.ShouldHaveSingleItem().ExplicitDefaultValue.ShouldBe(true);
        attribute.GetMembers("AutoIncrement").OfType<IPropertySymbol>().Single().SetMethod.ShouldNotBeNull();
    }

    private static void AssertComposite(string source, string first, string second)
    {
        source.ShouldContain($"PRIMARY KEY (\"{first}\", \"{second}\")");
        source.ShouldContain($"\"{first}\" = ${first} AND \"{second}\" = ${second}");
        source.ShouldContain($"conflictColumns = new string[] {{ \"{first}\", \"{second}\" }};");
        source.ShouldContain("public static void Insert(");
        source.ShouldNotContain("SelectDict");
        source.ShouldNotContain("GetIndex(");
        source.ShouldNotContain("Convert.ToInt32(commandResult)");
        source.ShouldNotContain("Convert.ToInt64(commandResult)");
        source.ShouldNotContain($"ADD COLUMN \"{first}\"");
        source.ShouldNotContain($"ADD COLUMN \"{second}\"");
        string plainInsert = source.Substring(source.IndexOf("public static void Insert(", StringComparison.Ordinal));
        plainInsert = plainInsert.Substring(0, plainInsert.IndexOf("InsertReturning(", StringComparison.Ordinal));
        plainInsert.ShouldNotContain($"value.{first} = ");
        plainInsert.ShouldNotContain($"value.{second} = ");
    }

    private static GeneratorRunResult Run(string properties, string attributes = "") => GeneratorTestHost.Run($$"""
        using CsLightDbGen.SQLiteGenerator;
        using K = CsLightDbGen.SQLiteGenerator.LdgSQLiteKeyNoUnique;
        namespace KeyTests;
        [LdgSQLiteTable]
        {{attributes}}
        public partial class Item
        {
            {{properties}}
            public string Label { get; set; } = string.Empty;
        }
        """);

    private static string Generate(string properties, string attributes = "")
    {
        GeneratorRunResult run = Run(properties, attributes);
        AssertValid(run);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "Item.Table.g.cs");
    }

    private static void AssertValid(GeneratorRunResult run)
    {
        run.DriverRunResult.Diagnostics.ShouldBeEmpty();
        run.Errors.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        GeneratorTestHost.EmitToReference(run.OutputCompilation).Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }
}
