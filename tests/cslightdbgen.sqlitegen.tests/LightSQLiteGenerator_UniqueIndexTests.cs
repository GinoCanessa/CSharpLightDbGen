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
    public void EnsureSchema_ReappliesClassLevelUniqueAsIndex()
    {
        string source = GetPackageSource();

        source.ShouldContain("CREATE UNIQUE INDEX IF NOT EXISTS \"UQ_{dbTableName}_Provider_Subject\"");
    }

    [Fact]
    public void ClassLevelUniqueIndex_EmittedForEnsureSchemaButNotCreateTable()
    {
        string source = GetPackageSource();

        // The re-assertion index lives only inside EnsureSchema; CreateTable relies on the
        // table-level UNIQUE (Provider, Subject) constraint instead, so the index name appears once.
        CountOccurrences(source, "UQ_{dbTableName}_Provider_Subject").ShouldBe(1);
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

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
