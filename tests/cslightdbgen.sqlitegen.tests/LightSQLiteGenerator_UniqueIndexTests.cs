using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_UniqueIndexTests
{
    private static string GetPackageSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.UniqueIndexFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "PackageRecordSQLite.g.cs");
    }

    private static string GetBasicSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntitySQLite.g.cs");
    }

    [Fact]
    public void ClassLevelUnique_EmitsMultiColumnConstraint()
    {
        string source = GetPackageSource();

        source.ShouldContain("UNIQUE (Provider, Subject)");
    }

    [Fact]
    public void Index_EmitsUniqueAndPartialClause()
    {
        string source = GetPackageSource();

        source.ShouldContain("CREATE UNIQUE INDEX IF NOT EXISTS");
        source.ShouldContain(") WHERE Status NOT IN ('complete','failed')");
    }

    [Fact]
    public void UniqueIndex_ProducesNoGeneratorErrors()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.UniqueIndexFixture);

        run.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void PropertyLevelUnique_StillEmitsInlineColumnConstraint()
    {
        string source = GetBasicSource();

        source.ShouldContain("UniqueCode TEXT UNIQUE");
    }

    [Fact]
    public void PlainIndex_RemainsNonUniqueWithoutPredicate()
    {
        string source = GetBasicSource();

        source.ShouldContain("CREATE INDEX IF NOT EXISTS \"IDX_{dbTableName}_Name_ParentKey\"");
        source.ShouldNotContain("CREATE UNIQUE INDEX");
    }
}
