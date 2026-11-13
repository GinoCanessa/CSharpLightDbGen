using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Shouldly;
using GeneratorRunResult = cslightdbgen.sqlitegen.tests.TestInfrastructure.GeneratorRunResult;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_JsonOptionsTests
{
    [Theory]
    [InlineData("public static class Factory { public static JsonSerializerOptions Make() => new(); }", "Factory", "Make", "global::Demo.Factory.Make()")]
    [InlineData("internal class Factory { internal static JsonSerializerOptions? Make() => null; }", "Factory", "Make", "global::Demo.Factory.Make()")]
    [InlineData("public class Factory { protected internal static JsonSerializerOptions Make() => new(); }", "Factory", "Make", "global::Demo.Factory.Make()")]
    [InlineData("public class Outer { internal class Factory { internal static JsonSerializerOptions Make() => new(); } }", "Outer.Factory", "Make", "global::Demo.Outer.Factory.Make()")]
    [InlineData("public class Factory<T> { public static JsonSerializerOptions Make() => new(); }", "Factory<int>", "Make", "global::Demo.Factory<int>.Make()")]
    [InlineData("public class Outer<T> { public class Factory<U> { public static JsonSerializerOptions Make() => new(); } }", "Outer<int>.Factory<string>", "Make", "global::Demo.Outer<int>.Factory<string>.Make()")]
    [InlineData("public static class @event { public static JsonSerializerOptions @return() => new(); }", "@event", "return", "global::Demo.@event.@return()")]
    [InlineData("public static class Factory { public static JsonSerializerOptions Make() => new(); public static int Make(int x = 0) => x; }", "Factory", "Make", "global::Demo.Factory.Make()")]
    [InlineData("public interface Factory { public static JsonSerializerOptions Make() => new(); }", "Factory", "Make", "global::Demo.Factory.Make()")]
    public void JsonFactory_ValidBindings_CompileQualifiedCalls(string factory, string type, string method, string invocation)
    {
        GeneratorRunResult run = Run(factory, type, "\"" + method + "\"");
        AssertClean(run);
        string generated = ModelSource(run, "Item");
        generated.ShouldContain("returnedOptions = " + invocation);
        generated.ShouldContain("new JsonSerializerOptions(returnedOptions)");
        generated.ShouldContain("global::System.Threading.LazyThreadSafetyMode.ExecutionAndPublication");
        generated.ShouldNotContain("private static readonly JsonSerializerOptions _options");
    }

    [Theory]
    [InlineData("public class Factory { }", "Factory", "\"Make\"", "exactly one")]
    [InlineData("public class Factory { public static JsonSerializerOptions Make(int x = 0) => new(); }", "Factory", "\"Make\"", "zero-parameter")]
    [InlineData("public class Factory { public JsonSerializerOptions Make() => new(); }", "Factory", "\"Make\"", "static")]
    [InlineData("public class Factory { public static JsonSerializerOptions Make<T>() => new(); }", "Factory", "\"Make\"", "non-generic")]
    [InlineData("public class Factory { public static JsonSerializerOptions Make() => new(); public static JsonSerializerOptions Make<T>() => new(); }", "Factory", "\"Make\"", "exactly one")]
    [InlineData("public class Factory { public static object Make() => new(); }", "Factory", "\"Make\"", "JsonSerializerOptions")]
    [InlineData("public class Factory { public static object Make() => new JsonSerializerOptions(); }", "Factory", "\"Make\"", "JsonSerializerOptions")]
    [InlineData("public class Factory { private static JsonSerializerOptions Make() => new(); }", "Factory", "\"Make\"", "accessible")]
    [InlineData("public class Factory { protected static JsonSerializerOptions Make() => new(); }", "Factory", "\"Make\"", "accessible")]
    [InlineData("public class Factory { private protected static JsonSerializerOptions Make() => new(); }", "Factory", "\"Make\"", "accessible")]
    [InlineData("public class Outer { private class Factory { public static JsonSerializerOptions Make() => new(); } }", "Outer.Factory", "\"Make\"", "accessible")]
    [InlineData("public class Outer { protected class Factory { public static JsonSerializerOptions Make() => new(); } }", "Outer.Factory", "\"Make\"", "accessible")]
    [InlineData("file class Factory { public static JsonSerializerOptions Make() => new(); }", "Factory", "\"Make\"", "file-local")]
    [InlineData("public class Factory<T> { public static JsonSerializerOptions Make() => new(); }", "Factory<>", "\"Make\"", "closed")]
    [InlineData("public class Outer<T> { public class Factory { public static JsonSerializerOptions Make() => new(); } }", "Outer<>.Factory", "\"Make\"", "closed")]
    [InlineData("public interface Factory { static abstract JsonSerializerOptions Make(); }", "Factory", "\"Make\"", "directly invocable")]
    [InlineData("public interface Factory { static virtual JsonSerializerOptions Make() => new(); }", "Factory", "\"Make\"", "directly invocable")]
    [InlineData("public interface I { static abstract JsonSerializerOptions Make(); } public class Factory : I { static JsonSerializerOptions I.Make() => new(); }", "Factory", "\"Make\"", "exactly one")]
    [InlineData("public class Parent { public static JsonSerializerOptions Make() => new(); } public class Factory : Parent { }", "Factory", "\"Make\"", "declare")]
    [InlineData("public class Factory { public static JsonSerializerOptions Make() => new(); }", "Factory", "\"make\"", "exact name")]
    [InlineData("public class Factory { public static JsonSerializerOptions Make() => new(); }", "Factory", "\"Make(); throw null;\"", "exact name")]
    [InlineData("public class Factory { public static JsonSerializerOptions Make() => new(); }", "Factory", "\"\"", "FactoryMethod")]
    [InlineData("public class Factory { public static JsonSerializerOptions Make() => new(); }", "Factory", "null", "FactoryMethod")]
    [InlineData("", "int[]", "\"Make\"", "named type")]
    [InlineData("", "Missing", "\"Make\"", "named type")]
    public void JsonFactory_InvalidBindings_ReportCSLDG007(string factory, string type, string method, string reason)
    {
        GeneratorRunResult run = Run(factory, type, method);
        AssertInvalid(run, reason);
    }

    [Fact]
    public void JsonFactory_ReturnTypeUsesCompilationIdentity_NotDisplayName()
    {
        GeneratorRunResult library = GeneratorTestHost.Run(
            new GeneratorTestOptions { AssemblyName = "WrongOptionsIdentity" },
            "namespace System.Text.Json { public class JsonSerializerOptions { } }");
        EmittedReferenceResult image = GeneratorTestHost.EmitToReference(library.OutputCompilation);
        MetadataReference aliased = image.Reference.WithAliases(["foreign"]);
        GeneratorRunResult run = GeneratorTestHost.Run(
            new GeneratorTestOptions { AdditionalReferences = [aliased] },
            """
            extern alias foreign;
            using CsLightDbGen.SQLiteGenerator;
            namespace Demo;
            public static class Factory
            {
                public static foreign::System.Text.Json.JsonSerializerOptions Make() => new();
            }
            [LdgSQLiteTable, LdgSQLiteJsonOptions(typeof(Factory), "Make")]
            public partial class Item { public string Name { get; set; } = ""; }
            """);
        AssertInvalid(run, "this compilation's");
    }

    [Theory]
    [InlineData("public", "public", false, true)]
    [InlineData("public", "internal", false, false)]
    [InlineData("public", "internal", true, true)]
    [InlineData("internal", "public", false, false)]
    [InlineData("internal", "internal", true, true)]
    public void JsonFactory_ReferencedFactory_RespectsAssemblyAccessibility(string access, string methodAccess, bool friend, bool valid)
    {
        string librarySource = $$"""
            {{(friend ? "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"FactoryConsumer\")]" : "")}}
            namespace Library;
            {{access}} static class Factory
            {
                {{methodAccess}} static System.Text.Json.JsonSerializerOptions Make() => new();
            }
            """;
        CSharpCompilation library = GeneratorTestHost.CreateCompilation(
            new GeneratorTestOptions { AssemblyName = "FactoryLibrary" }, librarySource);
        EmittedReferenceResult image = GeneratorTestHost.EmitToReference(library);
        GeneratorRunResult run = GeneratorTestHost.Run(
            new GeneratorTestOptions { AssemblyName = "FactoryConsumer", AdditionalReferences = [image.Reference] },
            """
            using CsLightDbGen.SQLiteGenerator;
            namespace Demo;
            [LdgSQLiteTable, LdgSQLiteJsonOptions(typeof(Library.Factory), "Make")]
            public partial class Item { public Payload Value { get; set; } = new(); }
            public class Payload { }
            """);
        if (valid)
        {
            AssertClean(run);
            ModelSource(run, "Item").ShouldContain("global::Library.Factory.Make()");
        }
        else
        {
            AssertInvalid(run, "accessible");
        }
    }

    [Theory]
    [InlineData("private")]
    [InlineData("protected")]
    [InlineData("private protected")]
    public void JsonFactory_NestedContainerIsRejected_EvenWhenAnnotationCanNameIt(string access)
    {
        GeneratorRunResult run = GeneratorTestHost.Run($$"""
            using CsLightDbGen.SQLiteGenerator;
            namespace Demo;
            [LdgSQLiteTable, LdgSQLiteJsonOptions(typeof(Item.Factory), "Make")]
            public partial class Item
            {
                public string Text { get; set; } = "";
                {{access}} class Factory
                {
                    public static System.Text.Json.JsonSerializerOptions Make() => new();
                }
            }
            """);
        AssertInvalid(run, "accessible");
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void JsonFactory_QualifiedAnnotationAndEscapedNamespace_Compile()
    {
        GeneratorRunResult run = GeneratorTestHost.Run("""
            using CsLightDbGen.SQLiteGenerator;
            using JsonPolicy = CsLightDbGen.SQLiteGenerator.LdgSQLiteJsonOptions;
            namespace @namespace
            {
                public static class @class
                {
                    public static System.Text.Json.JsonSerializerOptions @new() => new();
                }
            }
            namespace Demo
            {
                public class Payload { }
                [LdgSQLiteTable, JsonPolicy(typeof(@namespace.@class), "new")]
                public partial class Item { public Payload Value { get; set; } = new(); }
            }
            """);
        AssertClean(run);
        ModelSource(run, "Item").ShouldContain("global::@namespace.@class.@new()");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonFactory_InvalidModelDoesNotSuppressUnrelatedModels(bool fts)
    {
        string invalidKind = fts ? "LdgSQLiteFtsTable(\"good\")" : "LdgSQLiteTable";
        GeneratorRunResult run = GeneratorTestHost.Run($$"""
            using CsLightDbGen.SQLiteGenerator;
            namespace Demo;
            public static class Factory { public static int Make() => 0; }
            [LdgSQLiteTable("good")]
            public partial class Good { public string Text { get; set; } = ""; }
            [{{invalidKind}}, LdgSQLiteJsonOptions(typeof(Factory), "Make")]
            public partial class Bad { public string Text { get; set; } = ""; }
            [LdgSQLiteFtsTable("good")]
            public partial class Search { public string Text { get; set; } = ""; }
            """);
        run.DriverRunResult.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("CSLDG007");
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedSources.Keys.ShouldContain(static name => name.EndsWith("Good.Table.g.cs", StringComparison.Ordinal));
        run.GeneratedSources.Keys.ShouldContain(static name => name.EndsWith("Search.Fts.g.cs", StringComparison.Ordinal));
        run.GeneratedSources.Keys.ShouldNotContain(static name => name.Contains("Bad.", StringComparison.Ordinal));
    }

    [Fact]
    public void JsonFactory_ValidUnusedBindingEmitsNoHelpers()
    {
        GeneratorRunResult run = Run("public static class Factory { public static JsonSerializerOptions Make() => throw new System.Exception(); }",
            "Factory", "\"Make\"", "public string Name { get; set; } = \"\";");
        AssertClean(run);
        ModelSource(run, "Item").ShouldNotContain("_jsonOptions");
        ModelSource(run, "Item").ShouldNotContain("TrySerializeForDb");
        ModelSource(run, "Item").ShouldNotContain("Factory.Make()");
    }

    [Fact]
    public void JsonFactory_TableAndFtsUseOneLazySnapshot()
    {
        GeneratorRunResult run = GeneratorTestHost.Run("""
            using CsLightDbGen.SQLiteGenerator;
            using System.Collections.Generic;
            namespace Demo;
            public class Payload { public string Text { get; set; } = ""; }
            public static class Factory { public static System.Text.Json.JsonSerializerOptions Make() => new(); }
            [LdgSQLiteTable("items"), LdgSQLiteJsonOptions(typeof(Factory), "Make")]
            public partial record class Item { public Payload Value { get; set; } = new(); public List<Payload> Items { get; set; } = []; }
            [LdgSQLiteFtsTable("items"), LdgSQLiteJsonOptions(typeof(Factory), "Make")]
            public partial class Search { public Payload Value { get; set; } = new(); public List<Payload> Items { get; set; } = []; }
            """);
        AssertClean(run);
        foreach (string model in new[] { "Item", "Search" })
        {
            string source = ModelSource(run, model);
            source.Split("private static readonly global::System.Lazy<JsonSerializerOptions>").Length.ShouldBe(2);
            source.Split("_jsonOptions.Value").Length.ShouldBe(5);
            source.ShouldContain("JsonSerializer.Serialize(instance, _jsonOptions.Value)");
            source.ShouldContain("JsonSerializer.Serialize(instances, _jsonOptions.Value)");
            source.ShouldContain("JsonSerializer.Deserialize<T>(json, _jsonOptions.Value)");
            source.ShouldContain("JsonSerializer.Deserialize<List<T>>(json, _jsonOptions.Value) ?? []");
            source.ShouldContain("string.IsNullOrWhiteSpace(json)");
        }
    }

    [Fact]
    public void JsonFactory_DoesNotInheritBaseOrSourcePolicy()
    {
        GeneratorRunResult run = GeneratorTestHost.Run("""
            using CsLightDbGen.SQLiteGenerator;
            namespace Demo;
            public class Payload { }
            public static class Factory { public static System.Text.Json.JsonSerializerOptions Make() => new(); }
            [LdgSQLiteBaseClass, LdgSQLiteJsonOptions(typeof(Factory), "Make")]
            public class Base { public Payload Value { get; set; } = new(); }
            [LdgSQLiteTable("inherited")]
            public partial class Inherited : Base { }
            [LdgSQLiteTable("selected"), LdgSQLiteJsonOptions(typeof(Factory), "Make")]
            public partial class Selected : Base { }
            [LdgSQLiteFtsTable("selected")]
            public partial class Search { public Payload Value { get; set; } = new(); }
            [LdgSQLiteTable("dual"), LdgSQLiteFtsTable("selected"), LdgSQLiteJsonOptions(typeof(Factory), "Make")]
            public partial class Dual { public Payload Value { get; set; } = new(); }
            """);
        AssertClean(run);
        ModelSource(run, "Inherited").ShouldContain("private static readonly JsonSerializerOptions _options");
        ModelSource(run, "Search").ShouldContain("private static readonly JsonSerializerOptions _options");
        ModelSource(run, "Selected").ShouldContain("_jsonOptions.Value");
        run.GeneratedSources.Keys.ShouldNotContain(static name => name.EndsWith("Dual.Fts.g.cs", StringComparison.Ordinal));
        ModelSource(run, "Dual").ShouldContain("_jsonOptions.Value");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonFactory_AnnotationSurface_IsDirectSingleUseAndConditionallyAccessible(bool local)
    {
        GeneratorRunResult run = GeneratorTestHost.Run(new GeneratorTestOptions
        {
            ParseOptions = local ? CSharpParseOptions.Default.WithPreprocessorSymbols("CSLIGHTDBGEN_MULTI_HOST") : CSharpParseOptions.Default,
        }, local ? "[assembly: CsLightDbGen.SQLiteGenerator.LdgSQLiteRuntimeNamespace(\"Json.Host.Runtime\")]" : "// default");
        AssertClean(run);
        INamedTypeSymbol attribute = run.OutputCompilation.GetTypeByMetadataName("CsLightDbGen.SQLiteGenerator.LdgSQLiteJsonOptions")!;
        attribute.DeclaredAccessibility.ShouldBe(local ? Accessibility.Internal : Accessibility.Public);
        foreach (string property in new[] { "FactoryType", "FactoryMethod" })
        {
            ((IPropertySymbol)attribute.GetMembers(property).Single()).SetMethod.ShouldBeNull();
        }
        AttributeData usage = attribute.GetAttributes().Single();
        usage.ConstructorArguments[0].Value.ShouldBe((int)AttributeTargets.Class);
        usage.NamedArguments.ShouldContain(static argument => argument.Key == "Inherited" && Equals(argument.Value.Value, false));
        usage.NamedArguments.ShouldContain(static argument => argument.Key == "AllowMultiple" && Equals(argument.Value.Value, false));
    }

    private static GeneratorRunResult Run(string factory, string type, string method, string? columns = null) =>
        GeneratorTestHost.Run($$"""
            using CsLightDbGen.SQLiteGenerator;
            using System.Text.Json;
            namespace Demo;
            {{factory}}
            public class Payload { public string Text { get; set; } = ""; }
            [LdgSQLiteTable, LdgSQLiteJsonOptions(typeof({{type}}), {{method}})]
            public partial class Item { {{columns ?? "public Payload Value { get; set; } = new();"}} }
            """);

    private static void AssertInvalid(GeneratorRunResult run, string reason)
    {
        Diagnostic diagnostic = run.DriverRunResult.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("CSLDG007");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage().ShouldContain(reason);
        diagnostic.GetMessage().ShouldContain("Item");
        AttributeSyntax annotation = run.InputCompilation.SyntaxTrees.Single().GetRoot().DescendantNodes()
            .OfType<AttributeSyntax>().Single(static attribute => attribute.Name.ToString() == "LdgSQLiteJsonOptions");
        diagnostic.Location.SourceSpan.ShouldBe(annotation.Span);
        run.GeneratedSources.Keys.ShouldNotContain(static name => name.EndsWith("Item.Table.g.cs", StringComparison.Ordinal));
        run.DriverRunResult.Diagnostics.ShouldNotContain(static diagnostic => diagnostic.Id == "CS8785");
    }

    private static string ModelSource(GeneratorRunResult run, string model) =>
        run.GeneratedSources.Single(pair => pair.Key.EndsWith(model + ".Table.g.cs", StringComparison.Ordinal) ||
            pair.Key.EndsWith(model + ".Fts.g.cs", StringComparison.Ordinal)).Value;

    private static void AssertClean(GeneratorRunResult run)
    {
        run.DriverRunResult.Diagnostics.ShouldBeEmpty();
        run.OutputDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        GeneratorTestHost.EmitToReference(run.OutputCompilation).Diagnostics
            .ShouldNotContain(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
