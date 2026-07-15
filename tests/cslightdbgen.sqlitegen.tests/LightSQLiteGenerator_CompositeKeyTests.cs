using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_CompositeKeyTests
{
    private static string GetCompositeSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.CompositePrimaryKeyFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "UserWebsite.Table.g.cs");
    }

    private static string GetBasicSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntity.Table.g.cs");
    }

    [Fact]
    public void CompositeKey_EmitsTableConstraint()
    {
        string source = GetCompositeSource();

        source.ShouldContain("PRIMARY KEY (UserId, WebsiteId)");
    }

    [Fact]
    public void CompositeKey_EmitsNotNullOnKeyColumnsWithoutInlineDirective()
    {
        string source = GetCompositeSource();

        source.ShouldContain("UserId INTEGER NOT NULL");
        source.ShouldContain("WebsiteId INTEGER NOT NULL");
        source.ShouldNotContain("UserId INTEGER UNIQUE PRIMARY KEY");
        source.ShouldNotContain("WebsiteId INTEGER UNIQUE PRIMARY KEY");
    }

    [Fact]
    public void CompositeKey_ProducesNoGeneratorErrors()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.CompositePrimaryKeyFixture);

        run.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void CompositeKey_InsertReturnsVoidAndIncludesKeyColumns()
    {
        string source = GetCompositeSource();

        source.ShouldContain("public static void Insert(");

        // Composite keys are non-identity, so the plain Insert does no auto-increment
        // round-trip: no identity RETURNING clause and no scalar key hydration.
        // (The dedicated InsertReturning/UpdateReturning methods emit RETURNING by design.)
        source.ShouldNotContain("Convert.ToInt64(commandResult)");
        source.ShouldNotContain("Convert.ToInt32(commandResult)");
    }

    [Fact]
    public void CompositeKey_OmitsSelectDict()
    {
        string source = GetCompositeSource();

        source.ShouldNotContain("SelectDict");
    }

    [Fact]
    public void CompositeKey_UpdateAndDeleteMatchOnAllKeyColumns()
    {
        string source = GetCompositeSource();

        source.ShouldContain("UserId = $UserId AND WebsiteId = $WebsiteId");
    }

    [Fact]
    public void SingleKey_RetainsInlineDirectiveSelectDictAndReturning()
    {
        string source = GetBasicSource();

        source.ShouldContain("Id INTEGER UNIQUE PRIMARY KEY NOT NULL");
        source.ShouldContain("SelectDict");
        source.ShouldContain("RETURNING Id");
        source.ShouldNotContain("PRIMARY KEY (Id)");
    }
}
