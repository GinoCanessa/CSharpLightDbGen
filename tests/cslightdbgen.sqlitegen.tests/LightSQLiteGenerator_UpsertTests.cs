using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_UpsertTests
{
    private static string GetBasicSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntity.Table.g.cs");
    }

    private static string GetCompositeSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.CompositePrimaryKeyFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "UserWebsite.Table.g.cs");
    }

    [Fact]
    public void Upsert_MethodAndExtensionAreGenerated()
    {
        string source = GetBasicSource();

        source.ShouldContain("public static void Upsert(");
        source.ShouldContain("public static void Upsert(this IDbConnection dbCon,");
    }

    [Fact]
    public void Upsert_EmitsOnConflictBranches()
    {
        string source = GetBasicSource();

        source.ShouldContain("ON CONFLICT({_conflictTarget}) DO NOTHING");
        source.ShouldContain("ON CONFLICT({_conflictTarget}) DO UPDATE SET");
        source.ShouldContain("excluded.");
    }

    [Fact]
    public void Upsert_EmitsIncrementForm()
    {
        string source = GetBasicSource();

        source.ShouldContain("+ excluded.");
    }

    [Fact]
    public void Upsert_IdentityPrimaryKey_RequiresExplicitConflictColumns()
    {
        string source = GetBasicSource();

        // An identity PK never collides on insert, so the model has no natural conflict
        // target and Upsert must reject a missing conflictColumns argument.
        source.ShouldContain("throw new System.ArgumentException(");
        source.ShouldContain("nameof(conflictColumns)");
    }

    [Fact]
    public void Upsert_CompositePrimaryKey_DefaultsConflictTargetToKeyColumns()
    {
        string source = GetCompositeSource();

        source.ShouldContain("conflictColumns = new string[] { \"UserId\", \"WebsiteId\" };");
    }

    [Fact]
    public void Upsert_ProducesNoGeneratorErrors()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);

        run.Errors.ShouldBeEmpty();
    }
}
