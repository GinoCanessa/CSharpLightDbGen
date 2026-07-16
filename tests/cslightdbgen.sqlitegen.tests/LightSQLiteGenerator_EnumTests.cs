using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_EnumTests
{
    [Fact]
    public void EnumColumn_EmitsEnumParseForScalar_AndJsonForCollection()
    {
        // H1: an enum scalar (Color) and a nullable enum scalar (Color?) route to Enum.Parse; an
        // enum collection (List<Color>) and an enum array (Color[]) route to the JSON-array helper
        // instead of being misclassified as enum scalars (which emitted Enum.Parse into a List<T>
        // property -> CS0029). The array additionally materializes via .ToArray().
        const string source = """
            using System.Collections.Generic;
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            public enum Color { Red, Green, Blue }

            [LdgSQLiteTable("enum_samples")]
            public partial class EnumSample
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public Color Primary { get; set; }

                public Color? Secondary { get; set; }

                public List<Color> Palette { get; set; } = new();

                public Color[] Swatches { get; set; } = [];
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "EnumSample.Table.g.cs");

        // Scalar + nullable scalar enums use Enum.Parse<Color>.
        generated.ShouldContain("Enum.Parse<CsLightDbGen.SQLiteGenerator.Color>");
        // The collection and array route to the JSON-array read helper, not Enum.Parse.
        generated.ShouldContain("ParseArrayFromDb<CsLightDbGen.SQLiteGenerator.Color>");
        // The array column materializes as Color[] via .ToArray(); List<Color> stays a List.
        generated.ShouldContain("ParseArrayFromDb<CsLightDbGen.SQLiteGenerator.Color>(reader.GetString(4)).ToArray()");

        // The whole generated DAL must compile: the assertion the prior text-only array test lacked.
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void EnumArray_MaterializesAsArray_NotList()
    {
        // B3: a T[] column previously emitted ParseArrayFromDb<T>() (a List<T>) into a T[] property
        // -> CS0029. It must now suffix .ToArray() so the read compiles.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            public enum Flag { A, B }

            [LdgSQLiteTable("flag_holder")]
            public partial class FlagHolder
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public Flag[] Flags { get; set; } = [];
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);
        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "FlagHolder.Table.g.cs");

        generated.ShouldContain(".ToArray()");
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void GenericStruct_WithUserTypeArg_ReportsCSLDG001_NotMisroutedAsEnum()
    {
        // H1: a non-System generic struct whose first type-arg is a user type (e.g. Wrapper<UserType>)
        // was misclassified as an enum scalar (Enum.Parse<UserType> -> broken). It is a value type
        // with no scalar mapping, so it must now report CSLDG001 and be skipped.
        const string source = """
            using CsLightDbGen.SQLiteGenerator;

            namespace CsLightDbGen.SQLiteGenerator;

            public class UserType { public int X { get; set; } }

            public struct Wrapper<T> { public T Value { get; set; } }

            [LdgSQLiteTable("wrapper_holder")]
            public partial class WrapperHolder
            {
                [LdgSQLiteKey]
                public int Id { get; set; }

                public Wrapper<UserType> Payload { get; set; }
            }
            """;

        GeneratorRunResult run = GeneratorTestHost.Run(source);

        run.Errors.ShouldContain(static d => d.Id == "CSLDG001");

        string generated = GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "WrapperHolder.Table.g.cs");
        generated.ShouldNotContain("Payload");
        run.CompilationErrors.ShouldBeEmpty();
    }
}
