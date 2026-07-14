using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_MultiSelectTests
{
    [Fact]
    public void ExplicitAttribute_EmitsValuesParameter_OnNonKeyProperty()
    {
        var run = GeneratorTestHost.Run(FixtureSources.MultiSelectFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "MultiSelectTargetSQLite.g.cs");

        source.ShouldContain("IEnumerable<string>? SlugValues = null");
        source.ShouldContain("Slug IN ");
    }

    [Fact]
    public void ExplicitAttribute_EmitsNotInValuesParameter_OnNonKeyProperty()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.MultiSelectFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "MultiSelectTargetSQLite.g.cs");

        source.ShouldContain("IEnumerable<string>? SlugNotInValues = null");
        source.ShouldContain("Slug NOT IN ");
    }

    [Fact]
    public void NotIn_UsesDistinctParameterNames_FromIn()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.MultiSelectFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "MultiSelectTargetSQLite.g.cs");

        // NOT IN binds its own parameters so an IN + NOT IN filter on the same column cannot
        // collide in command.Parameters.
        source.ShouldContain("SlugNotInParam");
        source.ShouldContain("SlugNotInValuesList");
    }

    [Fact]
    public void NonScalarProperty_WithMultiSelectAttribute_DoesNotEmitNotInValuesParameter()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.MultiSelectFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "MultiSelectTargetSQLite.g.cs");

        source.ShouldNotContain("TagsNotInValues");
    }

    [Fact]
    public void PrimaryKey_EmitsValuesParameter_Automatically()
    {
        var run = GeneratorTestHost.Run(FixtureSources.MultiSelectFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "MultiSelectTargetSQLite.g.cs");

        source.ShouldContain("IEnumerable<int>? IdValues = null");
        source.ShouldContain("Id IN ");
    }

    [Fact]
    public void LegacyKeyNameHeuristic_StillEmitsValuesParameter()
    {
        var run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntitySQLite.g.cs");

        source.ShouldContain("IEnumerable<int>? ParentKeyValues = null");
        source.ShouldContain("ParentKey IN ");
    }

    [Fact]
    public void NonScalarProperty_WithMultiSelectAttribute_DoesNotEmitValuesParameter()
    {
        var run = GeneratorTestHost.Run(FixtureSources.MultiSelectFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "MultiSelectTargetSQLite.g.cs");

        source.ShouldNotContain("TagsValues");
    }

    [Fact]
    public void Delete_SignatureIncludes_ValuesParameter()
    {
        var run = GeneratorTestHost.Run(FixtureSources.MultiSelectFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "MultiSelectTargetSQLite.g.cs");

        int deleteIdx = source.IndexOf("public static void Delete(\n");
        if (deleteIdx < 0)
        {
            deleteIdx = source.IndexOf("public static void Delete(\r\n");
        }
        deleteIdx.ShouldBeGreaterThan(-1);
        int bodyIdx = source.IndexOf('{', deleteIdx);
        string deleteSig = source.Substring(deleteIdx, bodyIdx - deleteIdx);

        deleteSig.ShouldContain("IEnumerable<int>? IdValues = null");
        deleteSig.ShouldContain("IEnumerable<string>? SlugValues = null");
    }

    [Fact]
    public void EnumerableMaterialization_IsPresentInGeneratedCode()
    {
        var run = GeneratorTestHost.Run(FixtureSources.MultiSelectFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "MultiSelectTargetSQLite.g.cs");

        source.ShouldContain("SlugValuesList");
        source.ShouldContain("as List<string>");
    }
}
