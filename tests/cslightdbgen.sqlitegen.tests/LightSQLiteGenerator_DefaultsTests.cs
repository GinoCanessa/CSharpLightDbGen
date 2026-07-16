using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_DefaultsTests
{
    private static string GetDefaultsEntitySource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.DefaultsFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DefaultsEntity.Table.g.cs");
    }

    [Theory]
    [InlineData("\"RetryCount\" INTEGER NOT NULL DEFAULT 0")]
    [InlineData("\"Status\" TEXT NOT NULL DEFAULT 'queued'")]
    [InlineData("\"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP")]
    [InlineData("\"IsActive\" INTEGER NOT NULL DEFAULT 1")]
    public void CreateTable_EmitsDefaultClause(string columnDefinition)
    {
        string source = GetDefaultsEntitySource();

        source.ShouldContain(columnDefinition);
    }

    [Fact]
    public void CreateTable_EscapesSingleQuotesInStringDefault()
    {
        string source = GetDefaultsEntitySource();

        source.ShouldContain("\"EscapedName\" TEXT NOT NULL DEFAULT 'O''Brien'");
    }

    [Fact]
    public void CreateTable_OmitsDefaultClauseWhenNoAttribute()
    {
        string source = GetDefaultsEntitySource();

        source.ShouldContain("\"NoDefault\" TEXT NOT NULL");
        source.ShouldNotContain("\"NoDefault\" TEXT NOT NULL DEFAULT");
    }

    [Fact]
    public void Generator_ProducesNoErrors_ForDefaultsFixture()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.DefaultsFixture);

        run.Errors.ShouldBeEmpty();
    }
}
