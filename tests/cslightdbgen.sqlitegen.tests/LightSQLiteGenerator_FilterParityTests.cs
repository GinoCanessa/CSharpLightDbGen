using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_FilterParityTests
{
    [Fact]
    public void GeneratedMethods_Include_CompareStringsWithLike_OnQueryMethods()
    {
        var run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntitySQLite.g.cs");

        source.ShouldContain("SelectSingle(");
        source.ShouldContain("SelectList(");
        source.ShouldContain("SelectEnumerable(");
        source.ShouldContain("SelectCount(");
        source.ShouldContain("Delete(");
        source.ShouldContain("bool compareStringsWithLike = false");
    }

    [Fact]
    public void GeneratedFilters_Include_NullTriState_ValuesIn_AndNumericOperators()
    {
        var run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntitySQLite.g.cs");

        source.ShouldContain("bool? OptionalScoreIsNull = null");
        source.ShouldContain("List<int>? ParentKeyValues = null");
        source.ShouldContain("string ParentKeyOperator");
        source.ShouldContain("getNumericOperator(ParentKeyOperator)");
        source.ShouldContain("ParentKey IN ");
        source.ShouldContain("vParamNames");
    }

    [Fact]
    public void GeneratedCode_Contains_SelectEnumerable_AndExtensionWrapper()
    {
        var run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntitySQLite.g.cs");

        source.ShouldContain("public static IEnumerable<BasicEntity> SelectEnumerable(");
        source.ShouldContain("public static IEnumerable<BasicEntity> SelectEnumerable<T>(");
        source.ShouldContain("return BasicEntity.SelectEnumerable(");
    }
}
