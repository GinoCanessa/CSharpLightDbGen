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

    private sealed class TestObj
    {
        public string Name { get; set; } = string.Empty;
    }
}
