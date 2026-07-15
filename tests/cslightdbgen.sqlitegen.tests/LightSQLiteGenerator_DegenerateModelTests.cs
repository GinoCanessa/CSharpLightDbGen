using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

// B1: degenerate model shapes (identity-only, keyless, primary-key-only) must generate valid,
// compilable SQL rather than the syntactically broken output the naive projections would produce.
public class LightSQLiteGenerator_DegenerateModelTests
{
    [Fact]
    public void IdentityOnlyModel_EmitsDefaultValuesInsert()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.IdentityOnlyFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "IdentityOnlyEntity.Table.g.cs");

        // With no insertable columns the INSERT must use "DEFAULT VALUES" rather than "() VALUES ()".
        source.ShouldContain("DEFAULT VALUES");
        source.ShouldNotContain("() VALUES ()");

        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void KeylessModel_EmitsNoOpByKeyPredicate()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.KeylessFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "KeylessEntity.Table.g.cs");

        // A keyless model has no row identity, so its by-key Update/Delete predicate must be a valid
        // no-op ("1 = 0"), never the invalid " = $" that a null key column would otherwise produce.
        source.ShouldContain("WHERE 1 = 0");
        source.ShouldNotContain("WHERE  = ");

        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void PrimaryKeyOnlyModel_EmitsSelfAssignSetClause()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.PrimaryKeyOnlyFixture);
        string source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "PrimaryKeyOnlyEntity.Table.g.cs");

        // No assignable (non-key) columns exist, so the UPDATE SET clause falls back to a harmless
        // self-assignment of the key column instead of emitting an empty (invalid) SET.
        source.ShouldContain("\"Code\" = \"Code\"");

        run.CompilationErrors.ShouldBeEmpty();
    }
}
