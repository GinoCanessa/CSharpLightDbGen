using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_ForeignKeyTests
{
    private static string GetFkChildSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.ForeignKeyActionsFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "FkChild.Table.g.cs");
    }

    private static string GetManifestEntrySource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.CompositeForeignKeyFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "ManifestEntry.Table.g.cs");
    }

    private static string GetFkResolveSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.ForeignKeyConstantResolutionFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "FkResolve.Table.g.cs");
    }

    [Fact]
    public void ForeignKey_EmitsOnDeleteAndOnUpdateActions()
    {
        string source = GetFkChildSource();

        source.ShouldContain("FOREIGN KEY (\"ParentId\") REFERENCES \"fk_parents\"(\"Id\") ON DELETE CASCADE ON UPDATE SET NULL");
    }

    [Fact]
    public void ForeignKey_OmitsActionClauseWhenDefault()
    {
        string source = GetFkChildSource();

        source.ShouldContain("FOREIGN KEY (\"OwnerId\") REFERENCES \"fk_owners\"(\"Id\")");
        source.ShouldNotContain("FOREIGN KEY (\"OwnerId\") REFERENCES \"fk_owners\"(\"Id\") ON");
    }

    [Fact]
    public void CompositeForeignKey_EmitsMultiColumnConstraint()
    {
        string source = GetManifestEntrySource();

        source.ShouldContain("FOREIGN KEY (\"TaskId\", \"ManifestGeneration\") REFERENCES \"task_manifests\" (\"TaskId\", \"Generation\") ON DELETE CASCADE");
    }

    [Fact]
    public void CompositeForeignKey_ProducesNoGeneratorErrors()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.CompositeForeignKeyFixture);

        run.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ForeignKey_NameofReferenceTable_ResolvesToConstantValue()
    {
        string source = GetFkResolveSource();

        source.ShouldContain("FOREIGN KEY (\"UserId\") REFERENCES \"Users\"(\"Id\")");
    }

    [Fact]
    public void ForeignKey_ConstStringReferenceTable_ResolvesToConstantValue()
    {
        string source = GetFkResolveSource();

        source.ShouldContain("FOREIGN KEY (\"AccountId\") REFERENCES \"accounts\"(\"Id\")");
    }

    [Fact]
    public void ForeignKey_VerbatimStringReferenceTable_ResolvesToConstantValue()
    {
        string source = GetFkResolveSource();

        source.ShouldContain("FOREIGN KEY (\"OrderId\") REFERENCES \"Orders\"(\"Id\")");
    }

    [Fact]
    public void ForeignKey_ConstantResolutionFixture_ProducesNoGeneratorErrors()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.ForeignKeyConstantResolutionFixture);

        run.Errors.ShouldBeEmpty();
    }
}
