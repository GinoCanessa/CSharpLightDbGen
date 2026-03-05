using FluentAssertions;

namespace cslightdbgen.sqlitegen.tests;

public class LdgSQLiteUtils_Tests
{
    [Theory]
    [InlineData(LdgSQLiteUtilsFixture.LdgNumericOperatorCodes.Equals, "=")]
    [InlineData(LdgSQLiteUtilsFixture.LdgNumericOperatorCodes.NotEquals, "!=")]
    [InlineData(LdgSQLiteUtilsFixture.LdgNumericOperatorCodes.GreaterThan, ">")]
    [InlineData(LdgSQLiteUtilsFixture.LdgNumericOperatorCodes.GreaterThanOrEquals, ">=")]
    [InlineData(LdgSQLiteUtilsFixture.LdgNumericOperatorCodes.LessThan, "<")]
    [InlineData(LdgSQLiteUtilsFixture.LdgNumericOperatorCodes.LessThanOrEquals, "<=")]
    public void GetSqlOperator_MapsAllEnumValues(LdgSQLiteUtilsFixture.LdgNumericOperatorCodes op, string expected)
    {
        LdgSQLiteUtilsFixture.GetSqlOperator(op).Should().Be(expected);
    }

    [Fact]
    public void GetSqlOperator_Throws_ForUnsupportedEnumValue()
    {
        var unsupported = (LdgSQLiteUtilsFixture.LdgNumericOperatorCodes)999;

        var act = () => LdgSQLiteUtilsFixture.GetSqlOperator(unsupported);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void TrySerializeForDb_Object_ReturnsFalseAndNull_ForNull()
    {
        var ok = LdgSQLiteUtilsFixture.TrySerializeForDb((TestObj?)null, out var json);

        ok.Should().BeFalse();
        json.Should().BeNull();
    }

    [Fact]
    public void TrySerializeForDb_Object_ReturnsTrueAndJson_ForValue()
    {
        var ok = LdgSQLiteUtilsFixture.TrySerializeForDb(new TestObj { Name = "abc" }, out var json);

        ok.Should().BeTrue();
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"Name\":\"abc\"");
    }

    [Fact]
    public void TrySerializeForDb_List_ReturnsFalse_ForNullOrEmpty()
    {
        var nullOk = LdgSQLiteUtilsFixture.TrySerializeForDb((List<TestObj>?)null, out var nullJson);
        var emptyOk = LdgSQLiteUtilsFixture.TrySerializeForDb(new List<TestObj>(), out var emptyJson);

        nullOk.Should().BeFalse();
        emptyOk.Should().BeFalse();
        nullJson.Should().BeNull();
        emptyJson.Should().BeNull();
    }

    [Fact]
    public void TrySerializeForDb_List_ReturnsTrue_ForNonEmpty()
    {
        var ok = LdgSQLiteUtilsFixture.TrySerializeForDb(new List<TestObj> { new() { Name = "x" } }, out var json);

        ok.Should().BeTrue();
        json.Should().Contain("\"Name\":\"x\"");
    }

    [Fact]
    public void ParseFromDb_ReturnsNull_ForWhitespace()
    {
        LdgSQLiteUtilsFixture.ParseFromDb<TestObj>("   ").Should().BeNull();
    }

    [Fact]
    public void ParseArrayFromDb_ReturnsEmpty_ForWhitespace()
    {
        LdgSQLiteUtilsFixture.ParseArrayFromDb<TestObj>("\t").Should().BeEmpty();
    }

    [Fact]
    public void StripHtml_RemovesTags_AndTrims()
    {
        var clean = LdgSQLiteUtilsFixture.StripHtml("  <p>Hello <strong>world</strong></p>  ");

        clean.Should().Be("Hello world");
    }

    private sealed class TestObj
    {
        public string Name { get; set; } = string.Empty;
    }
}
