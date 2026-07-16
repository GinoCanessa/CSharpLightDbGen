using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_EnsureSchemaTests
{
    private static string GetDefaultsSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.DefaultsFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DefaultsEntity.Table.g.cs");
    }

    [Fact]
    public void EnsureSchema_MethodIsGenerated()
    {
        string source = GetDefaultsSource();

        source.ShouldContain("public static bool EnsureSchema(IDbConnection dbConnection, string? dbTableName = null, IDbTransaction? transaction = null)");
    }

    [Fact]
    public void EnsureSchema_ExtensionWrapperIsGenerated()
    {
        string source = GetDefaultsSource();

        source.ShouldContain("public static bool EnsureSchema<T>(this IDbConnection dbCon, string? dbTableName = null, IDbTransaction? transaction = null)");
    }

    [Fact]
    public void EnsureSchema_QueriesExistingColumns()
    {
        string source = GetDefaultsSource();

        source.ShouldContain("PRAGMA table_info(");
        source.ShouldContain("existingColumns.Add(reader.GetString(1));");
    }

    [Fact]
    public void EnsureSchema_GuardsEachAlterOnExistingColumns()
    {
        string source = GetDefaultsSource();

        source.ShouldContain("if (!existingColumns.Contains(\"RetryCount\"))");
    }

    [Theory]
    [InlineData("ALTER TABLE {_tableIdent} ADD COLUMN \"RetryCount\" INTEGER DEFAULT 0 NOT NULL")]
    [InlineData("ALTER TABLE {_tableIdent} ADD COLUMN \"Status\" TEXT DEFAULT 'queued' NOT NULL")]
    [InlineData("ALTER TABLE {_tableIdent} ADD COLUMN \"IsActive\" INTEGER DEFAULT 1 NOT NULL")]
    [InlineData("ALTER TABLE {_tableIdent} ADD COLUMN \"EscapedName\" TEXT DEFAULT 'O''Brien' NOT NULL")]
    public void EnsureSchema_AddsConstantDefaultColumnsWithNotNull(string fragment)
    {
        string source = GetDefaultsSource();

        source.ShouldContain(fragment);
    }

    [Fact]
    public void EnsureSchema_RelaxesNotNullForRawDefaultColumns()
    {
        string source = GetDefaultsSource();

        // A raw (expression) default is not a permitted ADD COLUMN default in SQLite,
        // so the column is added with neither the default nor NOT NULL.
        source.ShouldContain("ADD COLUMN \"CreatedAt\" TEXT");
        source.ShouldNotContain("ADD COLUMN \"CreatedAt\" TEXT DEFAULT");
        source.ShouldNotContain("ADD COLUMN \"CreatedAt\" TEXT NOT NULL");
    }

    [Fact]
    public void EnsureSchema_RelaxesNotNullForColumnsWithoutConstantDefault()
    {
        string source = GetDefaultsSource();

        source.ShouldContain("ADD COLUMN \"NoDefault\" TEXT");
        source.ShouldNotContain("ADD COLUMN \"NoDefault\" TEXT NOT NULL");
    }

    [Fact]
    public void EnsureSchema_DoesNotAddPrimaryKeyColumn()
    {
        string source = GetDefaultsSource();

        source.ShouldNotContain("ADD COLUMN \"Id\" INTEGER");
    }

    [Fact]
    public void EnsureSchema_ProducesNoGeneratorErrors()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.DefaultsFixture);

        run.Errors.ShouldBeEmpty();
    }
}
