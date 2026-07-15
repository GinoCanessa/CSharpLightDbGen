using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;
using Microsoft.CodeAnalysis;
using System.Reflection;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_GenerationTests
{
    [Fact]
    public void Initialize_Emits_PostInitialization_Sources()
    {
        var run = GeneratorTestHost.Run("namespace T; public class Placeholder { }");

        run.GeneratedSources.Keys.ShouldContain("LdgSQLiteGeneratorAttributes.g.cs");
        // run.GeneratedSources.Keys.ShouldContain("LdgSQLiteUtils.g.cs");
    }

    [Fact]
    public void TablePath_Generates_ExpectedTableArtifacts()
    {
        var run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntitySQLite.g.cs");

        source.ShouldContain("CREATE TABLE IF NOT EXISTS");
        source.ShouldContain("UNIQUE");
        source.ShouldContain("ParentForeignKey");
        source.ShouldContain("REFERENCES");
        source.ShouldContain("_indexValue");
        source.ShouldContain("GetIndex() => Interlocked.Increment(ref _indexValue)");
        source.ShouldContain("public static HashSet<string> SQLiteColumnNames");
        source.ShouldContain("\"Id\"");
        source.ShouldContain("\"UniqueCode\"");
        source.ShouldContain("ResolveOrderByProperties");
        source.ShouldContain("SQLiteColumnNames.Contains(orderByProperty)");
        source.ShouldNotContain("IgnoredNote");
    }

    [Fact]
    public void IndexAttribute_Generates_CreateIndex_WithExpectedName()
    {
        var run = GeneratorTestHost.Run(FixtureSources.TableWithIndexFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "IndexedEntitySQLite.g.cs");

        source.ShouldContain("CREATE INDEX IF NOT EXISTS");
        source.ShouldContain("IDX_{dbTableName}_");
        source.ShouldContain("\"ColA\"");
        source.ShouldContain("\"ColB\"");
    }

    [Fact]
    public void RecordPath_Generates_RecordClassPartial()
    {
        var run = GeneratorTestHost.Run(FixtureSources.RecordTableFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "RecordEntitySQLite.g.cs");

        source.ShouldContain("public partial record class RecordEntity");
        run.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Inheritance_Collects_BaseClassMembers()
    {
        var run = GeneratorTestHost.Run(FixtureSources.InheritanceFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DerivedEntitySQLite.g.cs");

        source.ShouldContain("BaseName");
        source.ShouldContain("DerivedName");
    }

    [Fact]
    public void JsonAndArrayProperties_UseJsonUtilityMethods()
    {
        var run = GeneratorTestHost.Run(FixtureSources.TableWithJsonFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "JsonEntitySQLite.g.cs");

        source.ShouldContain("TrySerializeForDb(Payload");
        source.ShouldContain("TrySerializeForDb(PayloadTags");
        source.ShouldContain("ParseFromDb<");
        source.ShouldContain("JsonPayload>");
        source.ShouldContain("ParseArrayFromDb<");
        source.ShouldContain("JsonTag>");
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

        var run = GeneratorTestHost.Run(source);
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "ArrayRoutingEntitySQLite.g.cs");

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

        var run = GeneratorTestHost.Run(partOne, partTwo);
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "SplitEntitySQLite.g.cs");

        generated.ShouldContain("\"FirstPart\"");
        generated.ShouldContain("\"SecondPart\"");
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

        var run = GeneratorTestHost.Run(source);

        var errors = run.CompilationErrors.ToList();
        errors.ShouldBeEmpty(
            $"Fixture '{fixtureName}' produced {errors.Count} compilation error(s):\n" +
            string.Join("\n", errors.Select(static e => e.ToString())));
    }
}
