using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_ForeignKeyTests
{
    private static string GetFkChildSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.ForeignKeyActionsFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "FkChildSQLite.g.cs");
    }

    private static string GetManifestEntrySource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.CompositeForeignKeyFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "ManifestEntrySQLite.g.cs");
    }

    [Fact]
    public void ForeignKey_EmitsOnDeleteAndOnUpdateActions()
    {
        string source = GetFkChildSource();

        source.ShouldContain("FOREIGN KEY (ParentId) REFERENCES \"fk_parents\"(\"Id\") ON DELETE CASCADE ON UPDATE SET NULL");
    }

    [Fact]
    public void ForeignKey_OmitsActionClauseWhenDefault()
    {
        string source = GetFkChildSource();

        source.ShouldContain("FOREIGN KEY (OwnerId) REFERENCES \"fk_owners\"(\"Id\")");
        source.ShouldNotContain("FOREIGN KEY (OwnerId) REFERENCES \"fk_owners\"(\"Id\") ON");
    }

    [Fact]
    public void CompositeForeignKey_EmitsMultiColumnConstraint()
    {
        string source = GetManifestEntrySource();

        source.ShouldContain("FOREIGN KEY (TaskId, ManifestGeneration) REFERENCES task_manifests (TaskId, Generation) ON DELETE CASCADE");
    }

    [Fact]
    public void CompositeForeignKey_ProducesNoGeneratorErrors()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.CompositeForeignKeyFixture);

        run.Errors.ShouldBeEmpty();
    }
}
