using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using FluentAssertions;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_FilterParityTests
{
    [Fact]
    public void GeneratedMethods_Include_CompareStringsWithLike_OnQueryMethods()
    {
        var run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntitySQLite.g.cs");

        source.Should().Contain("SelectSingle(");
        source.Should().Contain("SelectList(");
        source.Should().Contain("SelectEnumerable(");
        source.Should().Contain("SelectCount(");
        source.Should().Contain("Delete(");
        source.Should().Contain("bool compareStringsWithLike = false");
    }

    [Fact]
    public void GeneratedFilters_Include_NullTriState_ValuesIn_AndNumericOperators()
    {
        var run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntitySQLite.g.cs");

        source.Should().Contain("bool? OptionalScoreIsNull = null");
        source.Should().Contain("List<int>? ParentKeyValues = null");
        source.Should().Contain("string ParentKeyOperator");
        source.Should().Contain("getNumericOperator(ParentKeyOperator)");
        source.Should().Contain("ParentKey IN ");
        source.Should().Contain("vParamNames");
    }

    [Fact]
    public void GeneratedCode_Contains_SelectEnumerable_AndExtensionWrapper()
    {
        var run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntitySQLite.g.cs");

        source.Should().Contain("public static IEnumerable<BasicEntity> SelectEnumerable(");
        source.Should().Contain("public static IEnumerable<BasicEntity> SelectEnumerable<T>(");
        source.Should().Contain("return BasicEntity.SelectEnumerable(");
    }
}
