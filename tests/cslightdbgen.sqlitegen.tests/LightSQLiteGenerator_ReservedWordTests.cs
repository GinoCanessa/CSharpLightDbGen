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
}
