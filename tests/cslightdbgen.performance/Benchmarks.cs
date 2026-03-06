using BenchmarkDotNet.Attributes;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CsLightDbGen.SQLiteGenerator;

namespace cslightdbgen.performance;

[LdgSQLiteTable("bench_records")]
[LdgSQLiteIndex("SegmentKey")]
public partial class BenchRecord
{
    [LdgSQLiteKey]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SegmentKey { get; set; }

    public int? Score { get; set; }
}

public sealed class BenchDbContext(DbContextOptions<BenchDbContext> options) : DbContext(options)
{
    public DbSet<BenchRecord> BenchRecords => Set<BenchRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BenchRecord>(entity =>
        {
            entity.ToTable("bench_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedOnAdd();
            entity.Property(x => x.Name).IsRequired();
            entity.HasIndex(x => x.SegmentKey);
        });
    }
}

internal static class BenchmarkDataFactory
{
    public const int BulkInsertCount = 1000;
    public const int SeedCount = 5000;
    public const int TargetId = 2500;
    public const int TargetSegmentKey = 7;

    private const string CreateSchemaSql = """
        CREATE TABLE IF NOT EXISTS bench_records (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            SegmentKey INTEGER NOT NULL,
            Score INTEGER NULL
        );
        CREATE INDEX IF NOT EXISTS IDX_bench_records_SegmentKey ON bench_records (SegmentKey);
        """;

    private const string InsertSql = """
        INSERT INTO bench_records (Name, SegmentKey, Score)
        VALUES ($Name, $SegmentKey, $Score);
        """;

    public static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    public static BenchDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<BenchDbContext>()
            .UseSqlite(connection)
            .Options;

        return new BenchDbContext(options);
    }

    public static void EnsureSchema(SqliteConnection connection)
    {
        connection.Execute(CreateSchemaSql);
    }

    public static void Seed(SqliteConnection connection, IReadOnlyList<BenchRecord> rows)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = InsertSql;

        var name = command.CreateParameter();
        name.ParameterName = "$Name";
        command.Parameters.Add(name);

        var segmentKey = command.CreateParameter();
        segmentKey.ParameterName = "$SegmentKey";
        command.Parameters.Add(segmentKey);

        var score = command.CreateParameter();
        score.ParameterName = "$Score";
        command.Parameters.Add(score);

        command.Prepare();

        foreach (var row in rows)
        {
            name.Value = row.Name;
            segmentKey.Value = row.SegmentKey;
            score.Value = row.Score.HasValue ? row.Score.Value : DBNull.Value;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public static List<BenchRecord> CreateRows(int count, int startIndex = 0)
    {
        var rows = new List<BenchRecord>(count);
        for (var i = 0; i < count; i++)
        {
            var index = startIndex + i;
            rows.Add(new BenchRecord
            {
                Name = $"name-{index}",
                SegmentKey = index % 10,
                Score = index % 5 == 0 ? null : index % 100
            });
        }

        return rows;
    }

    public static BenchRecord CreateSingleInsertRow()
        => new()
        {
            Name = "single-insert",
            SegmentKey = 4,
            Score = 88
        };

    public static List<BenchRecord> CloneRows(IEnumerable<BenchRecord> rows)
        => rows.Select(row => new BenchRecord
        {
            Name = row.Name,
            SegmentKey = row.SegmentKey,
            Score = row.Score
        }).ToList();
}

[MemoryDiagnoser]
public abstract class BenchmarkScenarioBase
{
    protected SqliteConnection SqliteGenConnection = null!;
    protected SqliteConnection DapperConnection = null!;
    protected SqliteConnection DapperBuilderConnection = null!;
    protected SqliteConnection EfConnection = null!;

    protected void InitializeEmptyDatabases()
    {
        SqliteGenConnection = BenchmarkDataFactory.OpenConnection();
        DapperConnection = BenchmarkDataFactory.OpenConnection();
        DapperBuilderConnection = BenchmarkDataFactory.OpenConnection();
        EfConnection = BenchmarkDataFactory.OpenConnection();

        BenchmarkDataFactory.EnsureSchema(SqliteGenConnection);
        BenchmarkDataFactory.EnsureSchema(DapperConnection);
        BenchmarkDataFactory.EnsureSchema(DapperBuilderConnection);
        BenchmarkDataFactory.EnsureSchema(EfConnection);
    }

    protected void InitializeSeededDatabases()
    {
        InitializeEmptyDatabases();

        var seedRows = BenchmarkDataFactory.CreateRows(BenchmarkDataFactory.SeedCount);

        BenchmarkDataFactory.Seed(SqliteGenConnection, seedRows);
        BenchmarkDataFactory.Seed(DapperConnection, seedRows);
        BenchmarkDataFactory.Seed(DapperBuilderConnection, seedRows);
        BenchmarkDataFactory.Seed(EfConnection, seedRows);
    }

    [IterationCleanup]
    public void Cleanup()
    {
        SqliteGenConnection.Dispose();
        DapperConnection.Dispose();
        DapperBuilderConnection.Dispose();
        EfConnection.Dispose();
    }

    protected static int ConsumeAll(IEnumerable<BenchRecord> records)
    {
        var count = 0;
        var checksum = 0;

        foreach (var record in records)
        {
            checksum ^= record.Id;
            checksum ^= record.SegmentKey;
            checksum ^= record.Score ?? 0;
            checksum ^= record.Name.Length;
            count++;
        }

        return count ^ checksum;
    }
}

public class SingleInsertBenchmarks : BenchmarkScenarioBase
{
    [IterationSetup]
    public void Setup() => InitializeEmptyDatabases();

    [Benchmark(Baseline = true)]
    public int SqliteGen()
    {
        var row = BenchmarkDataFactory.CreateSingleInsertRow();
        return BenchRecord.Insert(SqliteGenConnection, row);
    }

    [Benchmark]
    public int DapperDirect()
    {
        using var transaction = DapperConnection.BeginTransaction();

        var id = DapperConnection.ExecuteScalar<int>(
            """
            INSERT INTO bench_records (Name, SegmentKey, Score)
            VALUES (@Name, @SegmentKey, @Score)
            RETURNING Id;
            """,
            BenchmarkDataFactory.CreateSingleInsertRow(),
            transaction);

        transaction.Commit();
        return id;
    }

    [Benchmark]
    public int DapperSqlBuilder()
    {
        using var transaction = DapperBuilderConnection.BeginTransaction();

        var sqlBuilder = new SqlBuilder();
        var template = sqlBuilder.AddTemplate(
            """
            INSERT INTO bench_records (Name, SegmentKey, Score)
            VALUES (@Name, @SegmentKey, @Score)
            RETURNING Id;
            """);

        var id = DapperBuilderConnection.ExecuteScalar<int>(
            template.RawSql,
            BenchmarkDataFactory.CreateSingleInsertRow(),
            transaction);

        transaction.Commit();
        return id;
    }

    [Benchmark]
    public int EfCore()
    {
        using var context = BenchmarkDataFactory.CreateContext(EfConnection);

        var row = BenchmarkDataFactory.CreateSingleInsertRow();
        context.BenchRecords.Add(row);
        context.SaveChanges();

        return row.Id;
    }
}

public class BulkInsertBenchmarks : BenchmarkScenarioBase
{
    private List<BenchRecord> _rows = null!;

    [IterationSetup]
    public void Setup()
    {
        InitializeEmptyDatabases();
        _rows = BenchmarkDataFactory.CreateRows(BenchmarkDataFactory.BulkInsertCount, BenchmarkDataFactory.SeedCount + 1);
    }

    [Benchmark(Baseline = true)]
    public int SqliteGenList()
    {
        BenchRecord.Insert(SqliteGenConnection, BenchmarkDataFactory.CloneRows(_rows));
        return SqliteGenConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM bench_records;");
    }

    [Benchmark]
    public int SqliteGenEnumerable()
    {
        BenchRecord.Insert(SqliteGenConnection, BenchmarkDataFactory.CloneRows(_rows) as IEnumerable<BenchRecord>);
        return SqliteGenConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM bench_records;");
    }

    [Benchmark]
    public int DapperDirect()
    {
        using var transaction = DapperConnection.BeginTransaction();

        DapperConnection.Execute(
            """
            INSERT INTO bench_records (Name, SegmentKey, Score)
            VALUES (@Name, @SegmentKey, @Score);
            """,
            BenchmarkDataFactory.CloneRows(_rows),
            transaction);

        transaction.Commit();
        return DapperConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM bench_records;");
    }

    [Benchmark]
    public int DapperSqlBuilder()
    {
        using var transaction = DapperBuilderConnection.BeginTransaction();

        var sqlBuilder = new SqlBuilder();
        var template = sqlBuilder.AddTemplate(
            """
            INSERT INTO bench_records (Name, SegmentKey, Score)
            VALUES (@Name, @SegmentKey, @Score);
            """);

        DapperBuilderConnection.Execute(
            template.RawSql,
            BenchmarkDataFactory.CloneRows(_rows),
            transaction);

        transaction.Commit();
        return DapperBuilderConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM bench_records;");
    }

    [Benchmark]
    public int EfCore()
    {
        using var context = BenchmarkDataFactory.CreateContext(EfConnection);

        context.BenchRecords.AddRange(BenchmarkDataFactory.CloneRows(_rows));
        context.SaveChanges();

        return context.BenchRecords.Count();
    }
}

public class SingleRecordSelectBenchmarks : BenchmarkScenarioBase
{
    [IterationSetup]
    public void Setup() => InitializeSeededDatabases();

    [Benchmark(Baseline = true)]
    public int SqliteGen()
        => BenchRecord.SelectSingle(SqliteGenConnection, Id: BenchmarkDataFactory.TargetId)?.Id ?? -1;

    [Benchmark]
    public int DapperDirect()
        => DapperConnection.QuerySingleOrDefault<BenchRecord>(
            """
            SELECT Id, Name, SegmentKey, Score
            FROM bench_records
            WHERE Id = @Id
            LIMIT 1;
            """,
            new { Id = BenchmarkDataFactory.TargetId })?.Id ?? -1;

    [Benchmark]
    public int DapperSqlBuilder()
    {
        var sqlBuilder = new SqlBuilder();
        sqlBuilder.Where("Id = @Id", new { Id = BenchmarkDataFactory.TargetId });

        var template = sqlBuilder.AddTemplate(
            """
            SELECT Id, Name, SegmentKey, Score
            FROM bench_records
            /**where**/
            LIMIT 1;
            """);

        return DapperBuilderConnection.QuerySingleOrDefault<BenchRecord>(template.RawSql, template.Parameters)?.Id ?? -1;
    }

    [Benchmark]
    public int EfCore()
    {
        using var context = BenchmarkDataFactory.CreateContext(EfConnection);

        return context.BenchRecords
            .AsNoTracking()
            .SingleOrDefault(r => r.Id == BenchmarkDataFactory.TargetId)?.Id ?? -1;
    }
}

public class MultiRecordFilteredSelectBenchmarks : BenchmarkScenarioBase
{
    [IterationSetup]
    public void Setup() => InitializeSeededDatabases();

    [Benchmark(Baseline = true)]
    public int SqliteGen()
        => ConsumeAll(BenchRecord.SelectList(SqliteGenConnection, SegmentKey: BenchmarkDataFactory.TargetSegmentKey));

    [Benchmark]
    public int DapperDirect()
        => ConsumeAll(DapperConnection.Query<BenchRecord>(
            """
            SELECT Id, Name, SegmentKey, Score
            FROM bench_records
            WHERE SegmentKey = @SegmentKey;
            """,
            new { SegmentKey = BenchmarkDataFactory.TargetSegmentKey }));

    [Benchmark]
    public int DapperSqlBuilder()
    {
        var sqlBuilder = new SqlBuilder();
        sqlBuilder.Where("SegmentKey = @SegmentKey", new { SegmentKey = BenchmarkDataFactory.TargetSegmentKey });

        var template = sqlBuilder.AddTemplate(
            """
            SELECT Id, Name, SegmentKey, Score
            FROM bench_records
            /**where**/;
            """);

        return ConsumeAll(DapperBuilderConnection.Query<BenchRecord>(template.RawSql, template.Parameters));
    }

    [Benchmark]
    public int EfCore()
    {
        using var context = BenchmarkDataFactory.CreateContext(EfConnection);

        return ConsumeAll(context.BenchRecords
            .AsNoTracking()
            .Where(r => r.SegmentKey == BenchmarkDataFactory.TargetSegmentKey));
    }
}

public class MultiRecordUnfilteredSelectBenchmarks : BenchmarkScenarioBase
{
    [IterationSetup]
    public void Setup() => InitializeSeededDatabases();

    [Benchmark(Baseline = true)]
    public int SqliteGen()
        => ConsumeAll(BenchRecord.SelectList(SqliteGenConnection));

    [Benchmark]
    public int DapperDirect()
        => ConsumeAll(DapperConnection.Query<BenchRecord>(
            """
            SELECT Id, Name, SegmentKey, Score
            FROM bench_records;
            """));

    [Benchmark]
    public int DapperSqlBuilder()
    {
        var sqlBuilder = new SqlBuilder();
        var template = sqlBuilder.AddTemplate(
            """
            SELECT Id, Name, SegmentKey, Score
            FROM bench_records
            /**where**/;
            """);

        return ConsumeAll(DapperBuilderConnection.Query<BenchRecord>(template.RawSql, template.Parameters));
    }

    [Benchmark]
    public int EfCore()
    {
        using var context = BenchmarkDataFactory.CreateContext(EfConnection);

        return ConsumeAll(context.BenchRecords
            .AsNoTracking());
    }
}
