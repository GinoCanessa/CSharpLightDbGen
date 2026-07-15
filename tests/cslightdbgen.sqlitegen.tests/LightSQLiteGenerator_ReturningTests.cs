using cslightdbgen.sqlitegen.tests.TestFixtures;
using cslightdbgen.sqlitegen.tests.TestInfrastructure;
using Shouldly;

namespace cslightdbgen.sqlitegen.tests;

public class LightSQLiteGenerator_ReturningTests
{
    private static string GetBasicSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "BasicEntity.Table.g.cs");
    }

    private static string GetDefaultsSource()
    {
        GeneratorRunResult run = GeneratorTestHost.Run(FixtureSources.DefaultsFixture);
        return GeneratorTestHost.GetGeneratedSourceByHintSuffix(run, "DefaultsEntity.Table.g.cs");
    }

    [Fact]
    public void InsertReturning_MethodAndExtensionsAreGenerated()
    {
        string source = GetBasicSource();

        source.ShouldContain("public static BasicEntity InsertReturning(");
        source.ShouldContain("public static BasicEntity InsertReturning(this IDbConnection dbCon,");
        source.ShouldContain("public static BasicEntity InsertReturning(this BasicEntity value,");
    }

    [Fact]
    public void UpdateReturning_MethodAndExtensionsAreGenerated()
    {
        string source = GetBasicSource();

        source.ShouldContain("public static BasicEntity UpdateReturning(");
        source.ShouldContain("public static BasicEntity UpdateReturning(this IDbConnection dbCon,");
        source.ShouldContain("public static BasicEntity UpdateReturning(this BasicEntity value,");
    }

    [Fact]
    public void ReturningMethods_EmitFullColumnReturningAndHydration()
    {
        string source = GetBasicSource();

        // RETURNING lists every column so the model can be fully hydrated.
        source.ShouldContain("RETURNING Id, Name, ParentKey");
        // Hydration assigns straight back onto the passed instance.
        source.ShouldContain("value.Id = reader.GetInt32(0);");
        source.ShouldContain("value.Name = reader.GetString(1);");
    }

    [Fact]
    public void InsertReturning_OmitsRawDefaultColumnFromInsertButHydratesIt()
    {
        string source = GetDefaultsSource();

        int start = source.IndexOf("public static DefaultsEntity InsertReturning(");
        start.ShouldBeGreaterThan(-1);
        int end = source.IndexOf("public static void Insert(", start);
        end.ShouldBeGreaterThan(start);
        string method = source.Substring(start, end - start);

        // The raw CURRENT_TIMESTAMP default is computed by SQLite, so InsertReturning
        // neither lists nor parameterizes CreatedAt in the INSERT...
        method.ShouldNotContain("$CreatedAt");
        // ...but still returns and hydrates the database-computed value.
        method.ShouldContain("CreatedAt");
        method.ShouldContain("value.CreatedAt = reader.");
    }

    [Fact]
    public void InsertReturning_BindsConstantDefaultColumns()
    {
        string source = GetDefaultsSource();

        int start = source.IndexOf("public static DefaultsEntity InsertReturning(");
        int end = source.IndexOf("public static void Insert(", start);
        string method = source.Substring(start, end - start);

        // Constant defaults (RetryCount, Status) are still bound from the model instance.
        method.ShouldContain("$RetryCount");
        method.ShouldContain("$Status");
    }

    [Fact]
    public void ReturningMethods_ProduceNoGeneratorErrors()
    {
        GeneratorRunResult basicRun = GeneratorTestHost.Run(FixtureSources.BasicTableFixture);
        GeneratorRunResult defaultsRun = GeneratorTestHost.Run(FixtureSources.DefaultsFixture);

        basicRun.Errors.ShouldBeEmpty();
        defaultsRun.Errors.ShouldBeEmpty();
    }
}
