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
