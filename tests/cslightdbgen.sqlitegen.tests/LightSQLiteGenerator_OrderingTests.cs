using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;
using Microsoft.CodeAnalysis;
using GeneratorRunResult = cslightdbgen.sqlitegen.tests.TestInfrastructure.GeneratorRunResult;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_OrderingTests
{
    [Fact]
    public void OrderBy_ResolvesPerColumnDirection_NotSingleTrailingDirection()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntity.Table.g.cs");

        source.ShouldContain("ResolveOrderByProperties(string[]? orderByProperties, string[]? orderByDirections, string? orderByDirection)");
        source.ShouldContain("orderByProperty + (descending ? \" DESC\" : \" ASC\")");

        source.ShouldNotContain("command.CommandText += $\" DESC\"");
        source.ShouldNotContain("command.CommandText += $\" ASC\"");
    }

    [Fact]
    public void OrderBy_ResolvesDirectionByInputIndex_BeforeDroppingUnknownColumns()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntity.Table.g.cs");

        source.ShouldContain("for (int orderByIndex = 0; orderByIndex < orderByProperties.Length; orderByIndex++)");
        source.ShouldContain("!SQLiteColumnNames.Contains(orderByProperty)");
        source.ShouldContain("orderByDirections[orderByIndex]");
    }

    [Fact]
    public void OrderBy_SelectMethods_ExposeOrderByDirectionsParameter()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntity.Table.g.cs");

        source.ShouldContain("string[]? orderByDirections = null");
        CountOccurrences(source, "string[]? orderByDirections = null").ShouldBeGreaterThanOrEqualTo(4);
        source.ShouldContain("transaction, orderByDirections)");
    }

    [Fact]
    public void OrderBy_FtsSelect_ExposesOrderByDirections_AndPerColumnDirection()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.FtsFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "FtsEntity.Fts.g.cs");

        source.ShouldContain("string[]? orderByDirections = null");
        source.ShouldContain("orderByProperty + (descending ? \" DESC\" : \" ASC\")");
        source.ShouldNotContain("command.CommandText += $\" DESC\"");
    }

    [Fact]
    public void CompileContract_OrderingFixture_HasNoErrors()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);

        run.OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
