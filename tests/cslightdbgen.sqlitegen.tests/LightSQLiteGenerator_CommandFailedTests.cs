using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_CommandFailedTests
{
    [Fact]
    public void KeyedModel_ByKeyMutation_ThrowsTypedException_AndPreservesInsertGuard()
    {
        // M2/N1: a keyed model's by-key Update/Delete throws the typed LdgCommandFailedException on
        // a stale key (opt-out-able via throwOnZeroRowsAffected), the bare Exception("Command
        // failed!") is gone, and the Insert/Upsert path keeps its ignoreDuplicates guard.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            [LdgSQLiteTable("gadgets")]
            public partial class Gadget
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public string Label { get; set; } = string.Empty;
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "Gadget.Table.g.cs");

        // The typed exception replaces every bare System.Exception failure throw.
        generated.ShouldContain("LdgCommandFailedException");
        generated.ShouldNotContain("Command failed!");
        generated.ShouldNotContain("throw new Exception(");

        // Keyed by-key mutations gain the per-call opt-out and throw only when it is enabled.
        generated.ShouldContain("bool throwOnZeroRowsAffected = true");
        generated.ShouldContain("(rowsAffected == 0) && throwOnZeroRowsAffected");

        // The Insert/Upsert conflict path keeps its ignoreDuplicates guard (not made unconditional).
        generated.ShouldContain("!ignoreDuplicates");

        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void KeylessModel_ByKeyMutation_EmitsNoThrow_AndOmitsOptOut()
    {
        // M2: a keyless model's by-key predicate is "1 = 0", so its Update/Delete can never affect a
        // row. They must not throw and must omit the throwOnZeroRowsAffected opt-out entirely.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            [LdgSQLiteTable("keyless_events")]
            public partial class KeylessEvent
            {
                public string Message { get; set; } = string.Empty;

                public int Severity { get; set; }
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "KeylessEvent.Table.g.cs");

        // Keyless models never expose the opt-out and never throw on a by-key mutation.
        generated.ShouldNotContain("throwOnZeroRowsAffected");
        generated.ShouldContain("WHERE 1 = 0");

        run.CompilationErrors.ShouldBeEmpty();
    }
}
