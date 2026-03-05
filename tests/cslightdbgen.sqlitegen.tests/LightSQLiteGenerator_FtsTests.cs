using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_FtsTests
{
    [Fact]
    public void FtsPath_Generates_Fts5_Unindexed_Match_AndSanitizeCode()
    {
        var run = GeneratorTestHost.Run(FixtureSources.FtsFixture);
        var source = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "FtsEntitySQLite.g.cs");

        source.Should().Contain("CREATE VIRTUAL TABLE IF NOT EXISTS");
        source.Should().Contain("using fts5");
        source.Should().Contain("RawHtml UNINDEXED");
        source.Should().Contain("MATCH $matchTerm{index}");
        source.Should().Contain("matchParam.ParameterName = $\"$matchTerm{index}\"");
        source.Should().Contain("LdgSQLiteUtils.StripHtml(");
    }

    [Fact]
    public void CompileContract_FtsFixture_HasNoErrors()
    {
        var run = GeneratorTestHost.Run(FixtureSources.FtsFixture);

        run.OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
    }
}
