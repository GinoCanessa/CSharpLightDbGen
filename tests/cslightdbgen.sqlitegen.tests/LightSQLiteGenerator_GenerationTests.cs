using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CsLightDbGen.SQLiteGenerator;
using System.Reflection;
using GeneratorRunResult = cslightdbgen.sqlitegen.tests.TestInfrastructure.GeneratorRunResult;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_GenerationTests
{
    private const string DefaultContractSource = """
        using System.Collections.Generic;
        using CsLightDbGen.SQLiteGenerator;

        namespace Defaults;

        [LdgSQLiteTable("order")]
        public partial class DefaultRow
        {
            [LdgSQLiteKey]
            public int Id { get; set; }
            public string Select { get; set; } = string.Empty;
        }

        [LdgSQLiteTable("default_composite")]
        public partial class DefaultComposite
        {
            [LdgSQLiteKey]
            public int PartA { get; set; }
            [LdgSQLiteKey]
            public string? PartB { get; set; }
            public string Title { get; set; } = string.Empty;
        }

        [LdgSQLiteTable("default_keyless")]
        public partial class DefaultKeyless
        {
            public string Title { get; set; } = string.Empty;
        }

        public class Payload
        {
            public string Text { get; set; } = string.Empty;
        }

        [LdgSQLiteTable("default_json")]
        public partial class DefaultJson
        {
            [LdgSQLiteKey]
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public Payload Document { get; set; } = new Payload();
            public Payload? OptionalDocument { get; set; }
            public List<Payload> Items { get; set; } = [];
            public List<Payload>? OptionalItems { get; set; }
            public Payload[] Values { get; set; } = [];
            public Payload[]? OptionalValues { get; set; }
            public IEnumerable<Payload> Sequence { get; set; } = [];
        }

        [LdgSQLiteFtsTable("default_json")]
        public partial class DefaultSearch
        {
            public string Title { get; set; } = string.Empty;
            [LdgSQLiteFtsUnindexed]
            public Payload? OptionalDocument { get; set; }
            public List<Payload> Items { get; set; } = [];
            public Payload[] Values { get; set; } = [];
        }
        """;

    [Fact]
    public void Initialize_Emits_PostInitialization_Sources()
    {
        GeneratorRunResult run = GeneratorTestHost.Run("namespace T; public class Placeholder { }");

        run.GeneratedSources.Keys.ShouldContain("LdgSQLiteGeneratorAttributes.g.cs");

        // The post-init source must carry real content, not just exist: the attribute definitions
        // and the shared LdgCommandFailedException (with its ModelName/Sql surface) that the emitted
        // DAL throws on zero-row keyed mutations.
        string attributes = run.GeneratedSources["LdgSQLiteGeneratorAttributes.g.cs"];
        attributes.ShouldContain("class LdgSQLiteTable");
        attributes.ShouldContain("class LdgSQLiteKey");
        attributes.ShouldContain("class LdgSQLiteFtsTable");
        attributes.ShouldContain("public class LdgSQLiteTable : System.Attribute");
        attributes.ShouldContain("class LdgCommandFailedException");
        attributes.ShouldContain("public string ModelName");
        attributes.ShouldContain("public string Sql");
        attributes.ShouldContain("public sealed class LdgCommandFailedException : System.Exception");
        attributes.ShouldContain("public LdgCommandFailedException(string modelName, string sql)");
        attributes.ShouldContain("""
            : base($"The command for model '{modelName}' affected no rows (expected at least one). SQL: {sql}")
            """);
        attributes.ShouldContain("ModelName = modelName;");
        attributes.ShouldContain("Sql = sql;");
        attributes.ShouldContain("public string ModelName { get; }");
        attributes.ShouldContain("public string Sql { get; }");
        run.GeneratedSources.Keys.ShouldNotContain("LdgSQLiteRuntimeHelpers.g.cs");

        INamedTypeSymbol? exception = run.OutputCompilation.GetTypeByMetadataName(
            "CsLightDbGen.SQLiteGenerator.LdgCommandFailedException");
        exception.ShouldNotBeNull();
        exception.DeclaredAccessibility.ShouldBe(Accessibility.Public);
        exception.IsSealed.ShouldBeTrue();
        exception.BaseType.ShouldNotBeNull();
        exception.BaseType.ToDisplayString().ShouldBe("System.Exception");
        exception.InstanceConstructors.ShouldHaveSingleItem();
        exception.InstanceConstructors[0].DeclaredAccessibility.ShouldBe(Accessibility.Public);
        exception.InstanceConstructors[0].Parameters.Select(static parameter => parameter.Type.SpecialType)
            .ShouldBe([SpecialType.System_String, SpecialType.System_String]);
        AssertSuccessfulRun(run);
    }

    [Fact]
    public void Defaults_KeepGeneratedTableAndFtsContracts()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(DefaultContractSource);
        AssertSuccessfulRun(run);

        string table = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DefaultRow.Table.g.cs");
        string signatures = NormalizeWhitespace(table);
        signatures.ShouldContain("public static int Insert( IDbConnection dbConnection, DefaultRow value, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)");
        signatures.ShouldContain("public static bool CreateTable(IDbConnection dbConnection, string? dbTableName = null)");
        signatures.ShouldContain("public static bool EnsureSchema(IDbConnection dbConnection, string? dbTableName = null, IDbTransaction? transaction = null)");
        signatures.ShouldContain("public static DefaultRow Update(IDbConnection dbConnection, DefaultRow value, string? dbTableName = null, IDbTransaction? transaction = null, bool throwOnZeroRowsAffected = true)");
        signatures.ShouldContain("public static void Delete(IDbConnection dbConnection, DefaultRow value, string? dbTableName = null, IDbTransaction? transaction = null, bool throwOnZeroRowsAffected = true)");
        signatures.ShouldContain("public static void Upsert( IDbConnection dbConnection, DefaultRow value, string[]? conflictColumns = null, string[]? updateColumns = null, string[]? incrementColumns = null, string? dbTableName = null, IDbTransaction? transaction = null)");
        signatures.ShouldContain("public static DefaultRow? InsertReturning(");
        signatures.ShouldContain("public static DefaultRow? UpdateReturning(");
        signatures.ShouldContain("public static DefaultRow? SelectSingle(");
        signatures.ShouldContain("public static List<DefaultRow> SelectList(");
        signatures.ShouldContain("public static IEnumerable<DefaultRow> SelectEnumerable(");
        signatures.ShouldContain("public static Dictionary<int, DefaultRow> SelectDict(");
        signatures.ShouldContain("public static int SelectCount(");
        signatures.ShouldContain("public static void Insert(this IDbConnection dbCon, DefaultRow value, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)");
        signatures.ShouldContain("public static void Insert(this DefaultRow value, IDbConnection dbCon, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)");
        table.ShouldContain("public static string DefaultTableName => \"order\";");
        table.ShouldContain("public static IReadOnlyCollection<string> SQLiteColumnNames");
        table.ShouldContain("\"Id\" INTEGER UNIQUE PRIMARY KEY NOT NULL");
        table.ShouldContain("\"Select\" TEXT NOT NULL");
        table.ShouldContain("""string _tableIdent = dbTableName is null ? "\"order\"" : dbTableName;""");
        table.ShouldContain("""SELECT \"Id\", \"Select\" FROM {_tableIdent}""");
        table.ShouldContain("\"Id\" = $Id");
        table.ShouldContain("RETURNING \"Id\"");
        table.ShouldContain("value.Id = Convert.ToInt32(commandResult);");
        table.ShouldContain("public static int GetIndex(string? dbTableName = null)");
        table.ShouldContain("public static void LoadMaxKey(");
        table.ShouldNotContain("TrySerializeForDb");
        table.ShouldNotContain("ParseFromDb<");
        table.ShouldNotContain("ParseArrayFromDb<");
        table.ShouldNotContain("JsonSerializerOptions");
        table.ShouldNotContain("StripHtml(");
        table.ShouldContain("if ((rowsAffected == 0) && throwOnZeroRowsAffected) throw new global::CsLightDbGen.SQLiteGenerator.LdgCommandFailedException(\"DefaultRow\", command.CommandText);");
        table.ShouldContain("if (!ignoreDuplicates && (rowsAffected == 0)) throw new global::CsLightDbGen.SQLiteGenerator.LdgCommandFailedException(\"DefaultRow\", command.CommandText);");
        table.ShouldContain("if (!ignoreDuplicates && (commandResult == null)) throw new global::CsLightDbGen.SQLiteGenerator.LdgCommandFailedException(\"DefaultRow\", command.CommandText);");

        string composite = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DefaultComposite.Table.g.cs");
        composite.ShouldContain("\"PartA\" INTEGER NOT NULL");
        composite.ShouldContain("\"PartB\" TEXT");
        composite.ShouldNotContain("\"PartB\" TEXT NOT NULL");
        composite.ShouldContain("PRIMARY KEY (\"PartA\", \"PartB\")");
        composite.ShouldNotContain("UNIQUE PRIMARY KEY");
        composite.ShouldContain("\"PartA\" = $PartA AND \"PartB\" = $PartB");
        composite.ShouldContain("PartAParam.Value = value.PartA;");
        composite.ShouldContain("PartBParam.Value = (value.PartB == null) ? DBNull.Value : value.PartB;");
        composite.ShouldContain("conflictColumns = new string[] { \"PartA\", \"PartB\" };");
        composite.ShouldContain("IEnumerable<int>? PartAValues = null");
        composite.ShouldContain("IEnumerable<string>? PartBValues = null");
        composite.ShouldNotContain("ALTER TABLE {_tableIdent} ADD COLUMN \"PartA\"");
        composite.ShouldNotContain("ALTER TABLE {_tableIdent} ADD COLUMN \"PartB\"");
        composite.ShouldContain("public static void Insert(");
        composite.ShouldNotContain("SelectDict");
        composite.ShouldNotContain("LoadMaxKey");
        composite.ShouldNotContain("Interlocked.Increment");
        composite.ShouldNotContain("Convert.ToInt32(commandResult)");

        string keyless = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DefaultKeyless.Table.g.cs");
        keyless.ShouldContain("public static void Insert(");
        keyless.ShouldContain("WHERE 1 = 0");
        keyless.ShouldNotContain("throwOnZeroRowsAffected");
        keyless.ShouldNotContain("UNIQUE PRIMARY KEY NOT NULL");
        keyless.ShouldNotContain("PRIMARY KEY (");
        keyless.ShouldNotContain("LoadMaxKey");
        keyless.ShouldNotContain("JsonSerializerOptions");

        string fts = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DefaultSearch.Fts.g.cs");
        string ftsSignatures = NormalizeWhitespace(fts);
        ftsSignatures.ShouldContain("public static int Populate( IDbConnection dbConnection, string? dbTableName = null, string? sourceTableName = null, bool sanitizeText = false, IDbTransaction? transaction = null)");
        ftsSignatures.ShouldContain("public static List<DefaultSearch> Select( IDbConnection dbConnection, List<string> matchTerms, string? dbTableName = null, string[]? orderByProperties = null, string? orderByDirection = null, IDbTransaction? transaction = null, string[]? orderByDirections = null)");
        ftsSignatures.ShouldContain("public static int SelectCount( IDbConnection dbConnection, List<string> matchTerms, string? dbTableName = null, IDbTransaction? transaction = null)");
        fts.ShouldContain("public static string DefaultTableName => \"DefaultSearch_fts\";");
        fts.ShouldContain("public static string SourceTableName => \"default_json\";");
        fts.ShouldContain("CREATE VIRTUAL TABLE IF NOT EXISTS {_tableIdent} using fts5");
        fts.ShouldContain("\"OptionalDocument\" UNINDEXED");
        fts.ShouldContain("MATCH $matchTerm{index}");
        fts.ShouldContain("matchParam.ParameterName = $\"$matchTerm{index}\";");
        fts.ShouldContain("if (sanitizeText == false)");
        fts.ShouldContain("StripHtml(");
        fts.ShouldContain("if (ra == 0) throw new global::CsLightDbGen.SQLiteGenerator.LdgCommandFailedException(\"DefaultSearch\", command.CommandText);");
    }

    [Fact]
    public void Defaults_KeepJsonWrappersAndReaderSentinels()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(DefaultContractSource);
        AssertSuccessfulRun(run);
        const string wrappers = """
            private static readonly JsonSerializerOptions _options = new()
            {
                WriteIndented = false,
            };

            private static bool TrySerializeForDb<T>(T? instance, [NotNullWhen(true)]out string? json) where T : class
            {
                if (instance == null)
                {
                    json = null;
                    return false;
                }

                json = JsonSerializer.Serialize(instance, _options);
                return true;
            }

            private static bool TrySerializeForDb<T>(List<T>? instances, [NotNullWhen(true)] out string? json)
            {
                if ((instances == null) || (instances.Count == 0))
                {
                    json = null;
                    return false;
                }

                json = JsonSerializer.Serialize(instances, _options);
                return true;
            }

            private static T? ParseFromDb<T>(string json) where T : class
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<T>(json, _options);
            }

            private static List<T> ParseArrayFromDb<T>(string json)
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    return [];
                }

                return JsonSerializer.Deserialize<List<T>>(json, _options) ?? [];
            }
            """;

        string table = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DefaultJson.Table.g.cs");
        string fts = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DefaultSearch.Fts.g.cs");
        NormalizeWhitespace(table).ShouldContain(NormalizeWhitespace(wrappers));
        NormalizeWhitespace(fts).ShouldContain(NormalizeWhitespace(wrappers));
        table.ShouldContain("Document = ParseFromDb<Defaults.Payload>(reader.GetString(2)) ?? new Defaults.Payload()");
        table.ShouldContain("OptionalDocument = reader.IsDBNull(3) ? null : ParseFromDb<Defaults.Payload>(reader.GetString(3))");
        table.ShouldContain("Items = ParseArrayFromDb<Defaults.Payload>(reader.GetString(4)) ?? []");
        table.ShouldContain("OptionalItems = reader.IsDBNull(5) ? null : ParseArrayFromDb<Defaults.Payload>(reader.GetString(5))");
        table.ShouldContain("Values = ParseArrayFromDb<Defaults.Payload>(reader.GetString(6)).ToArray()");
        table.ShouldContain("OptionalValues = reader.IsDBNull(7) ? null : ParseArrayFromDb<Defaults.Payload>(reader.GetString(7)).ToArray()");
        table.ShouldContain("Sequence = ParseArrayFromDb<Defaults.Payload>(reader.GetString(8)) ?? []");
        table.ShouldContain("DocumentParam.Value = TrySerializeForDb(value.Document, out dbJson) ? dbJson : string.Empty;");
        table.ShouldContain("OptionalDocumentParam.Value = TrySerializeForDb(value.OptionalDocument, out dbJson) ? dbJson : DBNull.Value;");
        table.ShouldContain("ItemsParam.Value = TrySerializeForDb(value.Items, out dbJson) ? dbJson : string.Empty;");
        table.ShouldContain("OptionalItemsParam.Value = TrySerializeForDb(value.OptionalItems, out dbJson) ? dbJson : DBNull.Value;");
        table.ShouldContain("ValuesParam.Value = TrySerializeForDb(value.Values, out dbJson) ? dbJson : string.Empty;");
        table.ShouldContain("OptionalValuesParam.Value = TrySerializeForDb(value.OptionalValues, out dbJson) ? dbJson : DBNull.Value;");
        table.ShouldContain("SequenceParam.Value = TrySerializeForDb(value.Sequence, out dbJson) ? dbJson : string.Empty;");
        table.ShouldContain("if (Document != null)");
        table.ShouldContain("if (OptionalDocument != null)");
        table.ShouldContain("if (OptionalDocumentIsNull == true)");
        table.ShouldContain("else if (OptionalDocumentIsNull == false)");
        table.ShouldContain("""\"OptionalDocument\" IS NULL""");
        table.ShouldContain("""\"OptionalDocument\" IS NOT NULL""");
        fts.ShouldContain("OptionalDocument = reader.IsDBNull(1) ? null : ParseFromDb<Defaults.Payload>(reader.GetString(1))");
        fts.ShouldContain("Items = ParseArrayFromDb<Defaults.Payload>(reader.GetString(2)) ?? []");
        fts.ShouldContain("Values = ParseArrayFromDb<Defaults.Payload>(reader.GetString(3)).ToArray()");
    }

    [Fact]
    public void CompilationHost_EmitsAndReferencesIndependentImages()
    {
        const string hostTemplate = """
            using CsLightDbGen.SQLiteGenerator;
            namespace HOST;
            [LdgSQLiteTable("items")]
            public partial class Item
            {
                [LdgSQLiteKey]
                public int Id { get; set; }
                public string Title { get; set; } = string.Empty;
            }
            """;
        GeneratorRunResult hostA = GeneratorTestHost.Run(
            new GeneratorTestOptions { AssemblyName = "IndependentHostA" },
            hostTemplate.Replace("HOST", "HostA", StringComparison.Ordinal));
        GeneratorRunResult hostB = GeneratorTestHost.Run(
            new GeneratorTestOptions { AssemblyName = "IndependentHostB" },
            hostTemplate.Replace("HOST", "HostB", StringComparison.Ordinal));
        EmittedReferenceResult imageA = AssertSuccessfulRun(hostA);
        EmittedReferenceResult imageB = AssertSuccessfulRun(hostB);
        hostA.Diagnostics.Concat(imageA.Diagnostics).ShouldNotContain(static diagnostic => diagnostic.Id == "CS0436" || diagnostic.Id == "CS0433");
        hostB.Diagnostics.Concat(imageB.Diagnostics).ShouldNotContain(static diagnostic => diagnostic.Id == "CS0436" || diagnostic.Id == "CS0433");

        // Default hosts still export the same annotations/helper. This ordinary consumer names only
        // their independently owned model APIs; distinct helper contracts belong to later phases.
        CSharpCompilation consumer = GeneratorTestHost.CreateCompilation(
            new GeneratorTestOptions
            {
                AssemblyName = "IndependentOrdinaryConsumer",
                AdditionalReferences = [imageA.Reference, imageB.Reference],
            },
            """
            using System.Data;
            public static class BothHosts
            {
                public static bool Create(IDbConnection connection) =>
                    HostA.Item.CreateTable(connection) && HostB.Item.CreateTable(connection);

                public static int InsertA(IDbConnection connection, HostA.Item item) =>
                    HostA.Item.Insert(connection, item);

                public static int InsertB(IDbConnection connection, HostB.Item item) =>
                    HostB.Item.Insert(connection, item);
            }
            """);
        consumer.GetDiagnostics().ShouldNotContain(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.Id == "CS0436" || diagnostic.Id == "CS0433");
        consumer.SyntaxTrees.ShouldHaveSingleItem();
        consumer.Assembly.GetTypeByMetadataName("CsLightDbGen.SQLiteGenerator.LdgSQLiteTable").ShouldBeNull();
        INamedTypeSymbol? modelA = consumer.GetTypeByMetadataName("HostA.Item");
        INamedTypeSymbol? modelB = consumer.GetTypeByMetadataName("HostB.Item");
        modelA.ShouldNotBeNull();
        modelB.ShouldNotBeNull();
        modelA.ContainingAssembly.Name.ShouldBe("IndependentHostA");
        modelB.ContainingAssembly.Name.ShouldBe("IndependentHostB");
        EmittedReferenceResult consumerImage = GeneratorTestHost.EmitToReference(consumer);
        consumerImage.Diagnostics.ShouldNotContain(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.Id == "CS0436" || diagnostic.Id == "CS0433");
    }

    [Fact]
    public void CompilationHost_UsesConsistentParseOptions()
    {
        const string model = """
            using CsLightDbGen.SQLiteGenerator;
            namespace ParseOptions;
            #if HOST_FEATURE
            [LdgSQLiteTable("enabled")]
            #else
            [LdgSQLiteTable("disabled")]
            #endif
            public partial class Item
            {
                [LdgSQLiteKey]
                public int Id { get; set; }
            }
            """;
        const string other = """
            namespace ParseOptions;
            public class Companion : External.Marker
            {
            #if HOST_FEATURE
                public int Enabled { get; set; }
            #else
                public int Disabled { get; set; }
            #endif
            }
            """;
        CSharpParseOptions parseOptions = new CSharpParseOptions(
            LanguageVersion.CSharp12,
            DocumentationMode.Diagnose,
            preprocessorSymbols: ["HOST_FEATURE"]);
        CSharpCompilation dependency = GeneratorTestHost.CreateCompilation(
            new GeneratorTestOptions { AssemblyName = "ParseOptionsDependency", ParseOptions = parseOptions },
            "namespace External; public class Marker { }");
        dependency.GetDiagnostics().ShouldNotContain(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        EmittedReferenceResult dependencyImage = GeneratorTestHost.EmitToReference(dependency);
        dependencyImage.Diagnostics.ShouldNotContain(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        GeneratorRunResult first = GeneratorTestHost.Run(
            new GeneratorTestOptions
            {
                AssemblyName = "ConsistentParseOptions",
                ParseOptions = parseOptions,
                AdditionalReferences = [dependencyImage.Reference],
                TrackIncrementalSteps = true,
            },
            model, other);
        AssertSuccessfulRun(first);
        first.InputCompilation.SyntaxTrees.ShouldAllBe(tree => tree.Options.Equals(parseOptions));
        first.OutputCompilation.SyntaxTrees.ShouldAllBe(tree => tree.Options.Equals(parseOptions));
        GeneratorTestHost.GetGeneratedSourceByHintSuffix(first, "Item.Table.g.cs")
            .ShouldContain("DefaultTableName => \"enabled\"");
        INamedTypeSymbol? companion = first.OutputCompilation.GetTypeByMetadataName("ParseOptions.Companion");
        companion.ShouldNotBeNull();
        companion.GetMembers("Enabled").ShouldHaveSingleItem();

        GeneratorRunResult unchanged = GeneratorTestHost.ReRun(
            first,
            sourceReplacements: new Dictionary<int, string> { [1] = other + "\n// unrelated edit" });
        AssertSuccessfulRun(unchanged);
        unchanged.InputCompilation.SyntaxTrees.First().ShouldBeSameAs(first.InputCompilation.SyntaxTrees.First());
        unchanged.InputCompilation.SyntaxTrees.Last().ShouldNotBeSameAs(first.InputCompilation.SyntaxTrees.Last());
        unchanged.GeneratedSources.OrderBy(static pair => pair.Key).ShouldBe(first.GeneratedSources.OrderBy(static pair => pair.Key));
        unchanged.DriverRunResult.Results[0].TrackedSteps[LightSQLiteGenerator.TableModelTrackingName]
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ShouldAllBe(static reason => reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged);

        CSharpParseOptions updatedOptions = parseOptions
            .WithLanguageVersion(LanguageVersion.CSharp13)
            .WithPreprocessorSymbols((string[])[]);
        GeneratorRunResult updated = GeneratorTestHost.ReRun(unchanged, parseOptions: updatedOptions);
        AssertSuccessfulRun(updated);
        updated.InputCompilation.AssemblyName.ShouldBe("ConsistentParseOptions");
        updated.InputCompilation.References.ShouldContain(dependencyImage.Reference);
        updated.InputCompilation.SyntaxTrees.ShouldAllBe(tree => tree.Options.Equals(updatedOptions));
        updated.OutputCompilation.SyntaxTrees.ShouldAllBe(tree => tree.Options.Equals(updatedOptions));
        GeneratorTestHost.GetGeneratedSourceByHintSuffix(updated, "Item.Table.g.cs")
            .ShouldContain("DefaultTableName => \"disabled\"");
        INamedTypeSymbol? updatedCompanion = updated.OutputCompilation.GetTypeByMetadataName("ParseOptions.Companion");
        updatedCompanion.ShouldNotBeNull();
        updatedCompanion.GetMembers("Disabled").ShouldHaveSingleItem();
        updatedCompanion.GetMembers("Enabled").ShouldBeEmpty();
        updated.DriverRunResult.Results[0].TrackedSteps[LightSQLiteGenerator.TableModelTrackingName]
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ShouldContain(IncrementalStepRunReason.Modified);

        GeneratorRunResult replaced = GeneratorTestHost.ReRun(
            updated,
            sourceReplacements: new Dictionary<int, string>
            {
                [0] = model.Replace("\"disabled\"", "\"replacement\"", StringComparison.Ordinal),
            });
        AssertSuccessfulRun(replaced);
        GeneratorTestHost.GetGeneratedSourceByHintSuffix(replaced, "Item.Table.g.cs")
            .ShouldContain("DefaultTableName => \"replacement\"");
    }

    [Fact]
    public void CompilationHost_EmitFailureIncludesFullDiagnostics()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(
            new GeneratorTestOptions { AssemblyName = "InvalidImage" },
            """
            #warning emit sentinel
            public class Invalid { public MissingType Value { get; set; } }
            """);
        run.OutputDiagnostics.ShouldBeEmpty();
        run.Diagnostics.ShouldContain(static diagnostic => diagnostic.Id == "CS0246");
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => GeneratorTestHost.EmitToReference(run.OutputCompilation));
        exception.Message.ShouldContain("InvalidImage");
        exception.Message.ShouldContain("CS0246");
        exception.Message.ShouldContain("CS1030");
        exception.Message.ShouldContain("emit sentinel");
    }

    private static EmittedReferenceResult AssertSuccessfulRun(GeneratorRunResult run)
    {
        Diagnostic[] errors = run.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        errors.ShouldBeEmpty(string.Join(Environment.NewLine, errors.Select(static diagnostic => diagnostic.ToString())));
        EmittedReferenceResult image = GeneratorTestHost.EmitToReference(run.OutputCompilation);
        image.Diagnostics.ShouldNotContain(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        return image;
    }

    private static string NormalizeWhitespace(string source) =>
        string.Join(" ", source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    [Fact]
    public void TablePath_Generates_ExpectedTableArtifacts()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntity.Table.g.cs");

        source.ShouldContain("CREATE TABLE IF NOT EXISTS");
        source.ShouldContain("UNIQUE");
        source.ShouldContain("ParentForeignKey");
        source.ShouldContain("REFERENCES");
        source.ShouldContain("_indexValues");
        source.ShouldContain("public static int GetIndex(string? dbTableName = null)");
        source.ShouldContain("Interlocked.Increment(ref _indexSlot(_tableIdent).Value)");
        source.ShouldContain("public static IReadOnlyCollection<string> SQLiteColumnNames");
        source.ShouldContain("\"Id\"");
        source.ShouldContain("\"UniqueCode\"");
        source.ShouldContain("ResolveOrderByProperties");
        source.ShouldContain("_sqliteColumnNames.Contains(orderByProperty)");
        source.ShouldNotContain("IgnoredNote");
    }

    [Fact]
    public void IndexAttribute_Generates_CreateIndex_WithExpectedName()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.TableWithIndexFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "IndexedEntity.Table.g.cs");

        source.ShouldContain("CREATE INDEX IF NOT EXISTS");
        source.ShouldContain("IDX_{dbTableName}_");
        source.ShouldContain("\"ColA\"");
        source.ShouldContain("\"ColB\"");
    }

    [Fact]
    public void RecordPath_Generates_RecordClassPartial()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.RecordTableFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "RecordEntity.Table.g.cs");

        source.ShouldContain("public partial record class RecordEntity");
        run.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Inheritance_Collects_BaseClassMembers()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.InheritanceFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DerivedEntity.Table.g.cs");

        source.ShouldContain("BaseName");
        source.ShouldContain("DerivedName");
    }

    [Fact]
    public void JsonAndArrayProperties_UseJsonUtilityMethods()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.TableWithJsonFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "JsonEntity.Table.g.cs");

        source.ShouldContain("TrySerializeForDb(Payload");
        source.ShouldContain("TrySerializeForDb(PayloadTags");
        source.ShouldContain("ParseFromDb<");
        source.ShouldContain("JsonPayload>");
        source.ShouldContain("ParseArrayFromDb<");
        source.ShouldContain("JsonTag>");
    }

    [Fact]
    public void ScalarOnlyModel_OmitsJsonAndHtmlStripHelpers()
    {
        // F2: the JSON (de)serialization helpers are emitted only for models carrying a JSON column,
        // and the HTML-strip helper is FTS-only. A scalar-only table must emit none of them.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;
            namespace T;

            [LdgSQLiteTable("scalar_only")]
            public partial class ScalarOnly
            {
                [LdgSQLiteKey]
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
                public int? Score { get; set; }
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "ScalarOnly.Table.g.cs");

        generated.ShouldNotContain("TrySerializeForDb");
        generated.ShouldNotContain("ParseFromDb<");
        generated.ShouldNotContain("ParseArrayFromDb<");
        generated.ShouldNotContain("JsonSerializerOptions");
        generated.ShouldNotContain("_htmlStripRegex");
        generated.ShouldNotContain("StripHtml(");
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Classification_ArrayAndValueTypeCollection_RouteCorrectly()
    {
        // Regression guard for A1: arrays (IArrayTypeSymbol) and value-type collections must be
        // classified as non-scalar array/collection columns (JSON[] read path -> ParseArrayFromDb),
        // NOT as single-object reference-JSON columns (ParseFromDb). Prior to symbol-based
        // classification, `int[]` fell through to the single-object bucket because arrays are not
        // INamedTypeSymbol.
        const string source = """
            using System.Collections.Generic;
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            [LdgSQLiteTable("array_routing")]
            public partial class ArrayRoutingEntity
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public int[] Numbers { get; set; } = [];

                public List<int> Scores { get; set; } = new();
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "ArrayRoutingEntity.Table.g.cs");

        // Both the array and the List<T> route to the JSON-array read directive...
        generated.ShouldContain("ParseArrayFromDb<System.Int32>");
        // ...and neither is misrouted to the single-object reference-JSON read directive.
        generated.ShouldNotContain("ParseFromDb<System.Int32>");
        generated.ShouldContain("\"Numbers\"");
        generated.ShouldContain("\"Scores\"");
    }

    [Fact]
    public void PartialClassAcrossSyntaxTrees_CollectsAllMembers()
    {
        // A1 switches member iteration to INamedTypeSymbol.GetMembers(), which spans every partial
        // declaration across all syntax trees. A property declared in a second partial file must be
        // collected as a column.
        const string partOne = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            [LdgSQLiteTable("split_table")]
            public partial class SplitEntity
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public string FirstPart { get; set; } = string.Empty;
            }
            """;

        const string partTwo = """
            namespace CsLightDbGen.SQLiteGenerator;

            public partial class SplitEntity
            {
                public string SecondPart { get; set; } = string.Empty;
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(partOne, partTwo);
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "SplitEntity.Table.g.cs");

        generated.ShouldContain("\"FirstPart\"");
        generated.ShouldContain("\"SecondPart\"");
        run.Errors.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void SameSimpleNameDifferentNamespaces_ProducesDistinctHints()
    {
        // A3: two [LdgSQLiteTable] types share the simple name "Widget" but live in different
        // namespaces. Hint names are namespace-qualified, so both are emitted without an
        // "hint name already added" collision.
        const string alpha = """
            using CsLightDbGen.SQLiteGenerator;

            namespace Alpha;

            [LdgSQLiteTable("alpha_widget")]
            public partial class Widget
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public string AlphaValue { get; set; } = string.Empty;
            }
            """;

        const string beta = """
            using CsLightDbGen.SQLiteGenerator;

            namespace Beta;

            [LdgSQLiteTable("beta_widget")]
            public partial class Widget
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public string BetaValue { get; set; } = string.Empty;
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(alpha, beta);

        List<string> widgetHints = run.GeneratedSources.Keys
            .Where(k => k.EndsWith("Widget.Table.g.cs", StringComparison.Ordinal))
            .ToList();

        widgetHints.ShouldContain("Alpha.Widget.Table.g.cs");
        widgetHints.ShouldContain("Beta.Widget.Table.g.cs");
        run.Errors.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Inheritance_Collects_BaseClassMembers_FromReferencedAssembly()
    {
        // A3: the base class lives in a referenced assembly (a metadata symbol with no syntax). Its
        // [LdgSQLiteBaseClass] members must still be collected as columns through the symbol walk.
        const string referencedBase = """
            using CsLightDbGen.SQLiteGenerator;

            namespace External;

            [LdgSQLiteBaseClass]
            public abstract class ExternalBase
            {
                public string BaseName { get; set; } = string.Empty;
            }
            """;

        const string derived = """
            using CsLightDbGen.SQLiteGenerator;
            using External;

            namespace Consumer;

            [LdgSQLiteTable("derived_external")]
            public partial class DerivedExternalEntity : ExternalBase
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public string DerivedName { get; set; } = string.Empty;
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.RunWithReference(referencedBase, derived);
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DerivedExternalEntity.Table.g.cs");

        generated.ShouldContain("BaseName");
        generated.ShouldContain("DerivedName");
        run.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void UnmappedValueType_EmitsCSLDG001()
    {
        // A2 / Decision 7: a value-type property with no scalar mapping (a custom struct) must be
        // reported as CSLDG001 and skipped, NOT routed into the reference-type-only JSON helpers
        // (which would emit non-compiling `ParseFromDb<Money>` where T : class).
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            public struct Money
            {
                public long Cents { get; set; }
            }

            [LdgSQLiteTable("unmapped_value")]
            public partial class UnmappedValueEntity
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public Money Price { get; set; }
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);

        run.Errors.ShouldContain(static d => d.Id == "CSLDG001");

        // The offending column is skipped, not misrouted to the reference-type-only JSON helpers,
        // and the remaining table source still compiles cleanly.
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "UnmappedValueEntity.Table.g.cs");
        generated.ShouldNotContain("Price");
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Key_AutoIncrementFalse_UsesSuppliedIntegerKey()
    {
        // A6: [LdgSQLiteKey(false)] on an integer single key must NOT be treated as an identity —
        // no counter, no LoadMaxKey, no RETURNING-driven overwrite. The caller supplies the key.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            [LdgSQLiteTable("supplied_keys")]
            public partial class SuppliedKeyEntity
            {
                [LdgSQLiteKey(false)]
                public int Id { get; set; }

                public string Label { get; set; } = string.Empty;
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);

        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "SuppliedKeyEntity.Table.g.cs");
        generated.ShouldNotContain("Interlocked.Increment");
        generated.ShouldNotContain("LoadMaxKey");
        run.Errors.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void ForeignKey_PositionalConstructorArguments_EmitConstraint()
    {
        // A6: the positional [LdgSQLiteForeignKey("table", "column")] form must emit the FK
        // constraint, exactly like the named ReferenceTable / ReferenceColumn spelling.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            [LdgSQLiteTable("positional_fk_children")]
            public partial class PositionalFkChild
            {
                [LdgSQLiteKey]
                public int ChildId { get; set; }

                [LdgSQLiteForeignKey("fk_parents", "Id", onDelete: LdgSQLiteFkAction.Cascade)]
                public int ParentRef { get; set; }
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);

        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "PositionalFkChild.Table.g.cs");
        generated.ShouldContain("FOREIGN KEY (\"ParentRef\") REFERENCES \"fk_parents\"(\"Id\") ON DELETE CASCADE");
        run.Errors.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void CompositeKey_ExplicitAutoIncrement_EmitsCSLDG003()
    {
        // A6 / Step 3: an explicit AutoIncrement request on a composite-key member is unsupported by
        // SQLite and must be diagnosed (a bare [LdgSQLiteKey] composite member is NOT flagged).
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            [LdgSQLiteTable("composite_auto")]
            public partial class CompositeAutoEntity
            {
                [LdgSQLiteKey(true)]
                public int PartA { get; set; }

                [LdgSQLiteKey]
                public int PartB { get; set; }

                public string Label { get; set; } = string.Empty;
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);

        run.Errors.ShouldContain(static d => d.Id == "CSLDG003");
        // Generation still produces a coherent composite-key table (AutoIncrement simply ignored).
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void ForeignKey_MissingReferenceColumn_EmitsCSLDG005()
    {
        // A6 / Step 3: an incomplete foreign key (reference table without a reference column) would
        // be silently dropped; it must be diagnosed instead.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            [LdgSQLiteTable("incomplete_fk")]
            public partial class IncompleteFkEntity
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                [LdgSQLiteForeignKey(ReferenceTable = "somewhere")]
                public int Ref { get; set; }
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);

        run.Errors.ShouldContain(static d => d.Id == "CSLDG005");
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void EnsureSchema_RequiredColumnWithoutConstantDefault_EmitsPopulatedTableGuard()
    {
        // A5: a NOT NULL column with no constant default is added nullable by ALTER TABLE, leaving
        // pre-existing rows NULL in a slot the readers hydrate as non-nullable. EnsureSchema must
        // fail fast on a populated table rather than reporting success.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            [LdgSQLiteTable("mig_required")]
            public partial class MigRequiredEntity
            {
                [LdgSQLiteKey]
                public int MigRequiredId { get; set; }

                public string MigRequiredLabel { get; set; } = string.Empty;
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);

        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "MigRequiredEntity.Table.g.cs");
        generated.ShouldContain("SELECT EXISTS(SELECT 1 FROM {_tableIdent})");
        generated.ShouldContain("EnsureSchema cannot add column 'MigRequiredLabel'");
        run.Errors.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void EnsureSchema_RawDefaultColumn_EmitsPopulatedTableGuard()
    {
        // A5: a raw (database-computed) default cannot be applied by ALTER TABLE ADD COLUMN, so a
        // non-nullable raw-default column left NULL on existing rows is also guarded at runtime.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            [LdgSQLiteTable("mig_raw")]
            public partial class MigRawEntity
            {
                [LdgSQLiteKey]
                public int MigRawId { get; set; }

                [LdgSQLiteDefault("CURRENT_TIMESTAMP", raw: true)]
                public string MigRawStamp { get; set; } = string.Empty;
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);

        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "MigRawEntity.Table.g.cs");
        generated.ShouldContain("EnsureSchema cannot add column 'MigRawStamp'");
        generated.ShouldContain("raw");
        run.Errors.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void EnsureSchema_NullableAndConstantDefaultColumns_HaveNoPopulatedTableGuard()
    {
        // A5: a nullable column (NULL is a valid hydrated value) and a NOT NULL column with a
        // constant default (backfilled by ALTER) are both additively migratable, so neither is
        // guarded — EnsureSchema adds them and keeps returning success.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            [LdgSQLiteTable("mig_safe")]
            public partial class MigSafeEntity
            {
                [LdgSQLiteKey]
                public int MigSafeId { get; set; }

                public string? MigSafeTag { get; set; }

                [LdgSQLiteDefault("0")]
                public int MigSafeCount { get; set; }
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);

        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "MigSafeEntity.Table.g.cs");
        generated.ShouldNotContain("EnsureSchema cannot add column");
        generated.ShouldNotContain("SELECT EXISTS(SELECT 1 FROM {_tableIdent})");
        run.Errors.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    public static IEnumerable<object[]> AllFixtureNames()
    {
        return typeof(FixtureSources)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(static f => new object[] { f.Name })
            .ToList();
    }

    [Theory]
    [MemberData(nameof(AllFixtureNames))]
    public void CompileContract_EveryFixture_ProducesNoCompilationErrors(string fixtureName)
    {
        FieldInfo? field = typeof(FixtureSources).GetField(fixtureName, BindingFlags.Public | BindingFlags.Static);
        field.ShouldNotBeNull();

        string source = (string)field!.GetRawConstantValue()!;

        GeneratorRunResult run = GeneratorTestHost.Run(source);

        List<Diagnostic> errors = run.CompilationErrors.ToList();
        errors.ShouldBeEmpty(
            $"Fixture '{fixtureName}' produced {errors.Count} compilation error(s):\n" +
            string.Join("\n", errors.Select(static e => e.ToString())));
    }
}
