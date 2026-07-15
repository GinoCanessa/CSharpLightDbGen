using System.Collections.Immutable;
using CsLightDbGen.SQLiteGenerator;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

// D2: the generator's attribute types stay PUBLIC (cross-assembly visible) rather than internal, and
// the supported multi-project topology is "host the analyzer in exactly one project, consume it from
// other projects via a metadata ProjectReference". A downstream consumer that references the
// generated project WITHOUT running the analyzer itself binds the host's public attributes and its
// generated public static DAL with no CS0436 duplicate-type conflict.
public class LightSQLiteGenerator_CrossProjectTests
{
    private const string HostSource = """
        using CsLightDbGen.SQLiteGenerator;

        namespace Data;

        [LdgSQLiteTable("customers")]
        public partial class Customer
        {
            [LdgSQLiteKey]
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }
        """;

    // Consumer references the host as metadata only (no analyzer) and binds:
    //   * typeof(LdgSQLiteTable) — proves the attribute type is PUBLIC across the assembly boundary
    //     (an internal attribute would not bind here),
    //   * Customer.CreateTable(IDbConnection) and Customer.SQLiteColumnNames — the generated public
    //     static DAL surface produced in the host project.
    private const string ConsumerSource = """
        using System;
        using System.Collections.Generic;
        using System.Data;
        using CsLightDbGen.SQLiteGenerator;
        using Data;

        namespace ConsumerApp;

        public static class CustomerUsage
        {
            public static Type TableAttributeType => typeof(LdgSQLiteTable);

            public static bool Create(IDbConnection connection) => Customer.CreateTable(connection);

            public static IReadOnlyCollection<string> Columns => Customer.SQLiteColumnNames;
        }
        """;

    // A second model, in the "main" project, that also carries the attribute. Used only by the
    // both-projects-host-the-analyzer scenario below.
    private const string SecondModelSource = """
        using CsLightDbGen.SQLiteGenerator;

        namespace Data2;

        [LdgSQLiteTable("orders")]
        public partial class Order
        {
            [LdgSQLiteKey]
            public int Id { get; set; }
        }
        """;

    [Fact]
    public void TwoProjectAnalyzerUse_HasNoAttributeTypeConflict()
    {
        (CSharpCompilation analyzerHost, CSharpCompilation consumer) =
            GeneratorTestHost.CompileConsumerReferencingGeneratedProject(HostSource, ConsumerSource);

        // The single project that runs the generator compiles cleanly: public attributes + DAL.
        analyzerHost.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();

        ImmutableArray<Diagnostic> consumerDiagnostics = consumer.GetDiagnostics();

        // The whole point of D2: consuming the generated project from a second project yields NO
        // duplicate attribute-type conflict, because only the host runs the analyzer.
        consumerDiagnostics.ShouldNotContain(d => d.Id == "CS0436");

        // The consumer binds the host's public attribute type and generated public static APIs with
        // no compilation errors at all.
        consumerDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();
    }

    [Fact]
    public void BothProjectsHostAnalyzer_ProducesCs0436AsWarningNotError()
    {
        // If BOTH projects run the analyzer, the downstream project acquires the attribute types
        // twice: once source-generated locally and once imported from the referenced (also-generated)
        // assembly. That collision is the CS0436 the recommended single-host topology avoids — and it
        // is a WARNING, not an error, so a consumer forced into this shape can silence it with
        // <NoWarn>$(NoWarn);CS0436</NoWarn> and still build.
        TestInfrastructure.GeneratorRunResult result = GeneratorTestHost.RunWithReference(HostSource, SecondModelSource);

        ImmutableArray<Diagnostic> diagnostics = result.OutputCompilation.GetDiagnostics();

        diagnostics.ShouldContain(d => d.Id == "CS0436");
        diagnostics
            .Where(d => d.Id == "CS0436")
            .ShouldAllBe(d => d.Severity == DiagnosticSeverity.Warning);
    }
}
