using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_ReservedWordTests
{
    private static string GetReservedWordSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.ReservedWordFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "ReservedWordEntity.Table.g.cs");
    }

    [Fact]
    public void ReservedWordColumns_AreQuotedInCreateTable()
    {
        string source = GetReservedWordSource();

        // CREATE TABLE column definitions are emitted into a raw string literal, so the SQL quotes
        // appear verbatim (bare) in the generated source.
        source.ShouldContain("\"Group\" TEXT");
        source.ShouldContain("\"Table\" INTEGER");
    }

    [Fact]
    public void ReservedWordColumns_AreQuotedInFilterPredicates()
    {
        string source = GetReservedWordSource();

        // Filter predicates are emitted into normal interpolated strings, so the SQL quotes are
        // C#-escaped in the generated source.
        source.ShouldContain("\\\"Group\\\" IN ");
        source.ShouldContain("\\\"Table\\\"");
    }

    [Fact]
    public void ReservedWordColumns_ProduceNoCompilationErrors()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.ReservedWordFixture);

        run.CompilationErrors.ShouldBeEmpty();
    }

    private static string GetReservedWordTableSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.ReservedWordTableFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "ReservedTableEntity.Table.g.cs");
    }

    [Fact]
    public void ReservedWordTableName_QuotesCompileTimeDefault()
    {
        string source = GetReservedWordTableSource();

        // Each generated method derives a quoted identifier from the ORIGINAL parameter nullness:
        // a null (compile-time default) becomes the quoted "Order"; a caller override is used raw.
        source.ShouldContain("dbTableName is null ? \"\\\"Order\\\"\" : dbTableName");
    }

    [Fact]
    public void ReservedWordTableName_UsesQuotedIdentAtBareSites()
    {
        string source = GetReservedWordTableSource();

        // Bare-identifier SQL sites reference the resolved _tableIdent instead of the raw name.
        source.ShouldContain("CREATE TABLE IF NOT EXISTS {_tableIdent} (");
        source.ShouldContain("DROP TABLE IF EXISTS {_tableIdent}");
        source.ShouldContain("FROM {_tableIdent}");
        source.ShouldContain("INTO {_tableIdent}");
    }

    [Fact]
    public void ReservedWordTableName_ProducesNoCompilationErrors()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.ReservedWordTableFixture);

        run.CompilationErrors.ShouldBeEmpty();
    }
}
