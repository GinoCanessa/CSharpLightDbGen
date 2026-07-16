using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LdgSQLiteUtils_Tests
{
    [Fact]
    public void TrySerializeForDb_Object_ReturnsFalseAndNull_ForNull()
    {
        var ok = LdgSQLiteUtilsFixture.TrySerializeForDb((TestObj?)null, out var json);

        ok.ShouldBeFalse();
        json.ShouldBeNull();
    }

    [Fact]
    public void TrySerializeForDb_Object_ReturnsTrueAndJson_ForValue()
    {
        var ok = LdgSQLiteUtilsFixture.TrySerializeForDb(new TestObj { Name = "abc" }, out var json);

        ok.ShouldBeTrue();
        json.ShouldNotBeNullOrWhiteSpace();
        json.ShouldContain("\"Name\":\"abc\"");
    }

    [Fact]
    public void TrySerializeForDb_List_ReturnsFalse_ForNullOrEmpty()
    {
        var nullOk = LdgSQLiteUtilsFixture.TrySerializeForDb((List<TestObj>?)null, out var nullJson);
        var emptyOk = LdgSQLiteUtilsFixture.TrySerializeForDb(new List<TestObj>(), out var emptyJson);

        nullOk.ShouldBeFalse();
        emptyOk.ShouldBeFalse();
        nullJson.ShouldBeNull();
        emptyJson.ShouldBeNull();
    }

    [Fact]
    public void TrySerializeForDb_List_ReturnsTrue_ForNonEmpty()
    {
        var ok = LdgSQLiteUtilsFixture.TrySerializeForDb(new List<TestObj> { new() { Name = "x" } }, out var json);

        ok.ShouldBeTrue();
        json!.ShouldContain("\"Name\":\"x\"");
    }

    [Fact]
    public void ParseFromDb_ReturnsNull_ForWhitespace()
    {
        LdgSQLiteUtilsFixture.ParseFromDb<TestObj>("   ").ShouldBeNull();
    }

    [Fact]
    public void ParseArrayFromDb_ReturnsEmpty_ForWhitespace()
    {
        LdgSQLiteUtilsFixture.ParseArrayFromDb<TestObj>("\t").ShouldBeEmpty();
    }

    [Fact]
    public void StripHtml_RemovesTags_AndTrims()
    {
        var clean = LdgSQLiteUtilsFixture.StripHtml("  <p>Hello <strong>world</strong></p>  ");

        clean.ShouldBe("Hello world");
    }

    [Fact]
    public void EmittedUtilities_MatchFixture()
    {
        // The DAL utility methods (StripHtml + the JSON serialize/parse helpers) are emitted inline
        // per generated class, while LdgSQLiteUtilsFixture is a hand-maintained public copy the other
        // unit tests exercise. This guard extracts each helper from freshly generated output and
        // asserts the fixture still mirrors it, so the two copies cannot silently drift apart.
        // The JSON helpers are emitted only for a model with a JSON column (BasicEntity), and
        // StripHtml only on the FTS path (FtsEntity); both are compared after normalizing the known,
        // intentional differences (private/public access, the emitted `_options`/`_htmlStripRegex`
        // field names vs the fixture's `Options`/`HtmlStripRegex`, `[NotNullWhen]`, and whitespace).
        string tableSource = GeneratorTestHost.GetGeneratedSourceByHintSuffix(
            GeneratorTestHost.Run(FixtureSources.BasicTableFixture), "BasicEntity.Table.g.cs");
        string ftsSource = GeneratorTestHost.GetGeneratedSourceByHintSuffix(
            GeneratorTestHost.Run(FixtureSources.FtsFixture), "FtsEntity.Fts.g.cs");
        string fixtureSource = File.ReadAllText(FixtureFilePath());

        string normFixture = NormalizeUtilities(fixtureSource);
        string normTable = NormalizeUtilities(tableSource);
        string normFts = NormalizeUtilities(ftsSource);

        // JSON helpers (+ options field) live on the regular table path.
        foreach (string anchor in new[]
        {
            "static readonly JsonSerializerOptions Options = new()",
            "static bool TrySerializeForDb<T>(T? instance,",
            "static bool TrySerializeForDb<T>(List<T>? instances,",
            "static T? ParseFromDb<T>(string json)",
            "static List<T> ParseArrayFromDb<T>(string json)",
        })
        {
            normTable.Contains(ExtractMember(normFixture, anchor), StringComparison.Ordinal)
                .ShouldBeTrue($"emitted DAL helper drifted from fixture: {anchor}");
        }

        // StripHtml (+ regex field) is emitted only on the FTS path.
        foreach (string anchor in new[]
        {
            "static readonly System.Text.RegularExpressions.Regex HtmlStripRegex = new(",
            "static string StripHtml(string? input)",
        })
        {
            normFts.Contains(ExtractMember(normFixture, anchor), StringComparison.Ordinal)
                .ShouldBeTrue($"emitted DAL helper drifted from fixture: {anchor}");
        }
    }

    private static string NormalizeUtilities(string source)
    {
        source = source.Replace("_options", "Options").Replace("_htmlStripRegex", "HtmlStripRegex");
        source = Regex.Replace(source, @"\[NotNullWhen\(true\)\]\s*", "");
        source = Regex.Replace(source, @"\b(public|private|internal)\s+", "");
        source = Regex.Replace(source, @"\s+", " ");
        return source.Trim();
    }

    // Extracts a single member from normalized source: from the anchor through either its terminating
    // ';' (for a field) or the matching close brace (for a method body).
    private static string ExtractMember(string normalized, string anchor)
    {
        int start = normalized.IndexOf(anchor, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"anchor not found in fixture: {anchor}");

        int semicolon = normalized.IndexOf(';', start);
        int brace = normalized.IndexOf('{', start);

        if (brace < 0 || (semicolon >= 0 && semicolon < brace))
        {
            return normalized.Substring(start, semicolon - start + 1);
        }

        int depth = 0;
        for (int i = brace; i < normalized.Length; i++)
        {
            if (normalized[i] == '{')
            {
                depth++;
            }
            else if (normalized[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return normalized.Substring(start, i - start + 1);
                }
            }
        }

        throw new InvalidOperationException($"Unterminated member for anchor: {anchor}");
    }

    private static string FixtureFilePath([CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "LdgSQLiteUtilsFixture.cs");

    private sealed class TestObj
    {
        public string Name { get; set; } = string.Empty;
    }
}
