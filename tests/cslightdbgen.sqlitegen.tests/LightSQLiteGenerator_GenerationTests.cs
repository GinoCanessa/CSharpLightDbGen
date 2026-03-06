using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_GenerationTests
{
    [Fact]
    public void Initialize_Emits_PostInitialization_Sources()
    {
        var run = GeneratorTestHost.Run("namespace T; public class Placeholder { }");

        run.GeneratedSources.Keys.Should().Contain("LdgSQLiteGeneratorAttributes.g.cs");
        // run.GeneratedSources.Keys.Should().Contain("LdgSQLiteUtils.g.cs");
    }

    [Fact]
    public void TablePath_Generates_ExpectedTableArtifacts()
    {
        var run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntitySQLite.g.cs");

        source.Should().Contain("CREATE TABLE IF NOT EXISTS");
        source.Should().Contain("UNIQUE");
        source.Should().Contain("ParentForeignKey");
        source.Should().Contain("REFERENCES");
        source.Should().Contain("_indexValue");
        source.Should().Contain("GetIndex() => Interlocked.Increment(ref _indexValue)");
        source.Should().NotContain("IgnoredNote");
    }

    [Fact]
    public void IndexAttribute_Generates_CreateIndex_WithExpectedName()
    {
        var run = GeneratorTestHost.Run(FixtureSources.TableWithIndexFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "IndexedEntitySQLite.g.cs");

        source.Should().Contain("CREATE INDEX IF NOT EXISTS");
        source.Should().Contain("IDX_{dbTableName}_");
        source.Should().Contain("\"ColA\"");
        source.Should().Contain("\"ColB\"");
    }

    [Fact]
    public void RecordPath_Generates_RecordClassPartial()
    {
        var run = GeneratorTestHost.Run(FixtureSources.RecordTableFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "RecordEntitySQLite.g.cs");

        source.Should().Contain("public partial record class RecordEntity");
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Inheritance_Collects_BaseClassMembers()
    {
        var run = GeneratorTestHost.Run(FixtureSources.InheritanceFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DerivedEntitySQLite.g.cs");

        source.Should().Contain("BaseName");
        source.Should().Contain("DerivedName");
    }

    [Fact]
    public void JsonAndArrayProperties_UseJsonUtilityMethods()
    {
        var run = GeneratorTestHost.Run(FixtureSources.TableWithJsonFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "JsonEntitySQLite.g.cs");

        source.Should().Contain("TrySerializeForDb(Payload");
        source.Should().Contain("TrySerializeForDb(PayloadTags");
        source.Should().Contain("ParseFromDb<");
        source.Should().Contain("JsonPayload>");
        source.Should().Contain("ParseArrayFromDb<");
        source.Should().Contain("JsonTag>");
    }

    [Fact]
    public void CompileContract_BasicFixture_HasNoErrors()
    {
        var run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);

        run.OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    [Fact]
    public void CompileContract_RecordFixture_HasNoErrors()
    {
        var run = GeneratorTestHost.Run(FixtureSources.RecordTableFixture);

        run.OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }

    [Fact]
    public void CompileContract_InheritanceFixture_HasNoErrors()
    {
        var run = GeneratorTestHost.Run(FixtureSources.InheritanceFixture);

        run.OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }
}
