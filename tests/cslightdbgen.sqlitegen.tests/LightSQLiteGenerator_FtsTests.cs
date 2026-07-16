using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;
using Microsoft.CodeAnalysis;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_FtsTests
{
    [Fact]
    public void FtsPath_Generates_Fts5_Unindexed_Match_AndSanitizeCode()
    {
        var run = GeneratorTestHost.Run(FixtureSources.FtsFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "FtsEntity.Fts.g.cs");

        source.ShouldContain("CREATE VIRTUAL TABLE IF NOT EXISTS");
        source.ShouldContain("using fts5");
        source.ShouldContain("\"RawHtml\" UNINDEXED");
        source.ShouldContain("MATCH $matchTerm{index}");
        source.ShouldContain("matchParam.ParameterName = $\"$matchTerm{index}\"");
        source.ShouldContain("public static IReadOnlyCollection<string> SQLiteColumnNames");
        source.ShouldContain("\"Title\"");
        source.ShouldContain("\"RawHtml\"");
        source.ShouldContain("ResolveOrderByProperties");
        source.ShouldContain("StripHtml(");
    }

    [Fact]
    public void CompileContract_FtsFixture_HasNoErrors()
    {
        var run = GeneratorTestHost.Run(FixtureSources.FtsFixture);

        run.OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void FtsPath_WithTokenizer_IncludesTokenizeClause()
    {
        var run = GeneratorTestHost.Run(FixtureSources.FtsTokenizerFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "FtsTokenizerEntity.Fts.g.cs");

        source.ShouldContain("CREATE VIRTUAL TABLE IF NOT EXISTS");
        source.ShouldContain("using fts5");
        source.ShouldContain("tokenize='porter ascii'");
    }

    [Fact]
    public void FtsPath_WithoutTokenizer_DoesNotIncludeTokenizeClause()
    {
        var run = GeneratorTestHost.Run(FixtureSources.FtsFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "FtsEntity.Fts.g.cs");

        source.ShouldNotContain("tokenize=");
    }

    [Fact]
    public void CompileContract_FtsTokenizerFixture_HasNoErrors()
    {
        var run = GeneratorTestHost.Run(FixtureSources.FtsTokenizerFixture);

        run.OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }

    [Fact]
    public void Fts_NonScalarColumn_Compiles()
    {
        // M3: an FTS model with a List<T> / complex-object column serializes those columns as JSON,
        // so the generated Populate/Select reference ParseArrayFromDb/ParseFromDb/_options. Before the
        // fix the FTS path never emitted emitJsonHelperMembers(), leaving those references undefined
        // (CS0103). The helpers must now be emitted whenever an FTS column is non-scalar.
        const string source = """
            using System.Collections.Generic;
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            public class Snippet { public string Text { get; set; } = string.Empty; }

            [LdgSQLiteTable("doc_source")]
            public partial class DocSource
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public string Title { get; set; } = string.Empty;
            }

            [LdgSQLiteFtsTable("doc_source")]
            public partial class DocSearch
            {
                public string Title { get; set; } = string.Empty;

                public List<string> Keywords { get; set; } = new();

                public Snippet? Lead { get; set; }
            }
            """;

        cslightdbgen.sqlitegen.tests.TestInfrastructure.GeneratorRunResult run = GeneratorTestHost.Run(source);
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DocSearch.Fts.g.cs");

        // The JSON read helpers are now present in the FTS class and referenced by the column reads.
        generated.ShouldContain("ParseArrayFromDb");
        generated.ShouldContain("ParseFromDb");
        generated.ShouldContain("private static List<T> ParseArrayFromDb<T>");

        // The whole generated FTS DAL compiles: no CS0103 for the JSON helpers.
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Fts_UnresolvedSource_ReportsCSLDG006()
    {
        // M5c: an FTS model whose source table matches no [LdgSQLiteTable] (in this compilation or a
        // referenced assembly) reports CSLDG006 and is skipped rather than emitting a broken DAL.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace Demo;

            [LdgSQLiteFtsTable("nonexistent_source")]
            public partial class OrphanSearch
            {
                public string Title { get; set; } = string.Empty;
            }
            """;

        cslightdbgen.sqlitegen.tests.TestInfrastructure.GeneratorRunResult run = GeneratorTestHost.Run(source);

        run.Errors.ShouldContain(static d => d.Id == "CSLDG006");
        run.GeneratedSources.Keys.ShouldNotContain(static key => key.EndsWith("OrphanSearch.Fts.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Fts_SourceInSameCompilation_NoDiagnostic()
    {
        // A source table declared in the SAME compilation resolves the FTS source: no CSLDG006, and
        // the FTS DAL is emitted.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace Demo;

            [LdgSQLiteTable("blog_posts")]
            public partial class BlogPost
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public string Title { get; set; } = string.Empty;
            }

            [LdgSQLiteFtsTable("blog_posts")]
            public partial class BlogPostSearch
            {
                public string Title { get; set; } = string.Empty;
            }
            """;

        cslightdbgen.sqlitegen.tests.TestInfrastructure.GeneratorRunResult run = GeneratorTestHost.Run(source);

        run.Errors.ShouldNotContain(static d => d.Id == "CSLDG006");
        run.GeneratedSources.Keys.ShouldContain(static key => key.EndsWith("BlogPostSearch.Fts.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Fts_SourceInReferencedAssembly_NoDiagnostic()
    {
        // A cross-project source table (declared in a referenced, also-generated assembly) must NOT
        // false-positive: the referenced-assembly scan resolves it, so no CSLDG006 and the FTS DAL is
        // still emitted.
        const string referencedSource = """
            using CsLightDbGen.SQLiteGenerator;

            namespace Data;

            [LdgSQLiteTable("blog_posts")]
            public partial class BlogPost
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public string Title { get; set; } = string.Empty;
            }
            """;

        const string ftsSource = """
            using CsLightDbGen.SQLiteGenerator;

            namespace Search;

            [LdgSQLiteFtsTable("blog_posts")]
            public partial class BlogPostSearch
            {
                public string Title { get; set; } = string.Empty;
            }
            """;

        cslightdbgen.sqlitegen.tests.TestInfrastructure.GeneratorRunResult run =
            GeneratorTestHost.RunWithReference(referencedSource, ftsSource);

        run.Errors.ShouldNotContain(static d => d.Id == "CSLDG006");
        run.GeneratedSources.Keys.ShouldContain(static key => key.EndsWith("BlogPostSearch.Fts.g.cs", StringComparison.Ordinal));
    }
}
