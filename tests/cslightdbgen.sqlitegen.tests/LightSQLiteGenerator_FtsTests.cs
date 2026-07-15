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
        source.ShouldContain("RawHtml UNINDEXED");
        source.ShouldContain("MATCH $matchTerm{index}");
        source.ShouldContain("matchParam.ParameterName = $\"$matchTerm{index}\"");
        source.ShouldContain("public static HashSet<string> SQLiteColumnNames");
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
}
