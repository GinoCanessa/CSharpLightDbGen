using CsLightDbGen.SQLiteGenerator;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_SyntaxTests
{
    [Fact]
    public void IsSyntaxTargetClassDec_ReturnsTrue_ForLdgSQLiteTable()
    {
        var classNode = ParseSingleClass("[LdgSQLiteTable] public class A { }");

        LightSQLiteGenerator.IsSyntaxTargetClassDec(classNode).Should().BeTrue();
    }

    [Fact]
    public void IsSyntaxTargetClassDec_ReturnsTrue_ForLdgSQLiteFtsTable()
    {
        var classNode = ParseSingleClass("[LdgSQLiteFtsTable(\"src\")] public class A { }");

        LightSQLiteGenerator.IsSyntaxTargetClassDec(classNode).Should().BeTrue();
    }

    [Fact]
    public void IsSyntaxTargetClassDec_ReturnsFalse_ForClassWithoutAttributes()
    {
        var classNode = ParseSingleClass("public class A { }");

        LightSQLiteGenerator.IsSyntaxTargetClassDec(classNode).Should().BeFalse();
    }

    [Fact]
    public void IsSyntaxTargetClassDec_ReturnsFalse_ForUnrelatedAttribute()
    {
        var classNode = ParseSingleClass("[Obsolete] public class A { }");

        LightSQLiteGenerator.IsSyntaxTargetClassDec(classNode).Should().BeFalse();
    }

    [Fact]
    public void IsSyntaxTargetRecordDec_ReturnsTrue_ForLdgSQLiteTable()
    {
        var recordNode = ParseSingleRecord("[LdgSQLiteTable] public record A;");

        LightSQLiteGenerator.IsSyntaxTargetRecordDec(recordNode).Should().BeTrue();
    }

    [Fact]
    public void IsSyntaxTargetRecordDec_ReturnsTrue_ForLdgSQLiteFtsTable()
    {
        var recordNode = ParseSingleRecord("[LdgSQLiteFtsTable(\"src\")] public record A;");

        LightSQLiteGenerator.IsSyntaxTargetRecordDec(recordNode).Should().BeTrue();
    }

    [Fact]
    public void IsSyntaxTargetRecordDec_ReturnsFalse_ForRecordWithoutAttributes()
    {
        var recordNode = ParseSingleRecord("public record A;");

        LightSQLiteGenerator.IsSyntaxTargetRecordDec(recordNode).Should().BeFalse();
    }

    [Fact]
    public void IsSyntaxTargetRecordDec_ReturnsFalse_ForUnrelatedAttribute()
    {
        var recordNode = ParseSingleRecord("[Serializable] public record A;");

        LightSQLiteGenerator.IsSyntaxTargetRecordDec(recordNode).Should().BeFalse();
    }

    private static ClassDeclarationSyntax ParseSingleClass(string declaration)
    {
        var tree = CSharpSyntaxTree.ParseText($"using CsLightDbGen.SQLiteGenerator; namespace Test; {declaration}");
        return tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
    }

    private static RecordDeclarationSyntax ParseSingleRecord(string declaration)
    {
        var tree = CSharpSyntaxTree.ParseText($"using CsLightDbGen.SQLiteGenerator; namespace Test; {declaration}");
        return tree.GetRoot().DescendantNodes().OfType<RecordDeclarationSyntax>().Single();
    }
}
