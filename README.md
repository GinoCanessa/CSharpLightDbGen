# CSharpLightDbGen

A **Roslyn incremental source generator** that emits strongly-typed SQLite data-access code at compile time for attributed C# models. Zero runtime reflection, zero runtime dependencies from the generator itself — just fast, predictable ADO.NET calls.

## Why?

| Concern | CSharpLightDbGen |
|---|---|
| **Performance** | Faster than Dapper for reads; 7–8× faster than EF Core |
| **Allocations** | Up to 47× fewer allocations than EF Core |
| **Runtime deps** | None from the generator; consumers only need `Microsoft.Data.Sqlite` |
| **API surface** | Full CRUD, filtered queries, FTS5 search — all generated |
| **Type safety** | Compile-time errors instead of runtime surprises |

## Quick Start

### 1. Install

Add the source generator to your project:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/cslightdbgen.sqlitegen.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

Add a runtime SQLite provider (e.g., `Microsoft.Data.Sqlite`).

### 2. Define a Model

```csharp
using CsLightDbGen.SQLiteGenerator;

[LdgSQLiteTable("customers")]
public partial class Customer
{
    [LdgSQLiteKey]
    public int CustomerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }

    public int SegmentKey { get; set; }

    public int? Score { get; set; }
}
```

The class must be `partial`. The generator will produce a companion partial with all data-access methods.

### 3. Use the Generated API

```csharp
using var db = new SqliteConnection("Data Source=:memory:");
db.Open();

// Create the table
Customer.CreateTable(db);

// Insert
var customer = new Customer { Name = "Alice", Age = 30, SegmentKey = 1 };
Customer.Insert(db, customer);

// Query
Customer? found = Customer.SelectSingle(db, Name: "Alice");
List<Customer> young = Customer.SelectList(db, AgeOperator: "<=", Age: 25);

// Extension methods work too
customer.Update(db);
customer.Delete(db);
```

## Attributes

| Attribute | Target | Purpose |
|---|---|---|
| `LdgSQLiteTable(tableName?, dynamicTableNames)` | Class | Generates full CRUD/query API for a SQLite table |
| `LdgSQLiteBaseClass` | Class | Marks a base class whose properties are inherited by table models |
| `LdgSQLiteKey(autoIncrement)` | Property | Designates the primary key column |
| `LdgSQLiteForeignKey(refTable?, refColumn?, modelTypeName?)` | Property | Declares a foreign key relationship |
| `LdgSQLiteIndex(columns...)` | Class | Emits a composite index on the specified columns |
| `LdgSQLiteUnique` | Property | Adds a `UNIQUE` constraint |
| `LdgSQLiteIgnore` | Property | Excludes the property from generation |
| `LdgSQLiteFtsTable(sourceTable, tableName?)` | Class | Generates an FTS5 full-text search table |
| `LdgSQLiteFtsUnindexed` | Property | Marks an FTS column as `UNINDEXED` |

## Generated API Surface

### Standard Table (`[LdgSQLiteTable]`)

**Schema:** `CreateTable`, `DropTable`

**Reads:** `SelectSingle`, `SelectList`, `SelectEnumerable`, `SelectDict`, `SelectCount`
- Filter by any property via named parameters
- Numeric operator filters (`Age`, `AgeOperator: ">="`)
- Nullable tri-state filters (`ScoreIsNull: true`)
- `IN`-list filters for `*Key` properties (`SegmentKeyValues: [1, 2, 3]`)
- `LIKE` matching for strings (`compareStringsWithLike: true`)
- Paging (`resultLimit`, `resultOffset`) and ordering (`orderByProperties`, `orderByDirection`)

**Writes:** `Insert`, `Update`, `Delete` — single value, list, or enumerable overloads
- `ignoreDuplicates` → `INSERT OR IGNORE`
- `insertPrimaryKey` → include PK in insert

**Utilities:** `LoadMaxKey`, `SelectMaxKey`

**Extensions:** convenience methods on `IDbConnection`, model instances, and collections

### FTS Table (`[LdgSQLiteFtsTable]`)

`CreateTable`, `DropTable`, `Populate`, `Select` (via `MATCH`), `SelectCount`
- Optional HTML stripping during `Populate(..., sanitizeText: true)`

## Full-Text Search Example

```csharp
[LdgSQLiteFtsTable("article_source")]
public partial class ArticleSearch
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    [LdgSQLiteFtsUnindexed]
    public string? RawHtml { get; set; }
}

// Populate FTS index from source table
ArticleSearch.Populate(db);

// Search
var results = ArticleSearch.Select(db, ["sqlite", "performance"]);
int count = ArticleSearch.SelectCount(db, ["sqlite"]);
```

## Record Class Support

Both `class` and `record class` models are supported:

```csharp
[LdgSQLiteTable("orders")]
public partial record class Order
{
    [LdgSQLiteKey]
    public int OrderId { get; set; }
    public int CustomerKey { get; set; }
    public string Description { get; set; } = string.Empty;
}
```

## Performance

Benchmarked against Dapper and EF Core on .NET 10 (BenchmarkDotNet v0.15.6, i9-13900KF):

### Single Record Select

| Method | Mean | Allocated |
|---|---|---|
| **CSharpLightDbGen** | **46.3 μs** | **1.43 KB** |
| Dapper (direct) | 55.3 μs | 1.72 KB |
| Dapper (SqlBuilder) | 76.5 μs | 3.32 KB |
| EF Core | 385.7 μs | 67.13 KB |

### Multi-Record Filtered Select

| Method | Mean | Allocated |
|---|---|---|
| **CSharpLightDbGen** | **322 μs** | **48.7 KB** |
| Dapper (direct) | 395 μs | 84.3 KB |
| EF Core | 661 μs | 198.4 KB |

### Multi-Record Unfiltered Select

| Method | Mean | Allocated |
|---|---|---|
| **CSharpLightDbGen** | **1.98 ms** | **520 KB** |
| Dapper (direct) | 2.66 ms | 849 KB |
| EF Core | 2.84 ms | 1,391 KB |

### Single Insert

| Method | Mean | Allocated |
|---|---|---|
| Dapper (direct) | 53.8 μs | 8.54 KB |
| **CSharpLightDbGen** | **67.1 μs** | **8.11 KB** |
| EF Core | 453.4 μs | 77.3 KB |

### Bulk Insert (1,000 rows)

| Method | Mean | Allocated |
|---|---|---|
| Dapper (direct) | 2.35 ms | 1,425 KB |
| **CSharpLightDbGen** | **3.90 ms** | **808 KB** |
| EF Core | 18.67 ms | 8,198 KB |

**Key takeaways:**
- **Reads:** 1.2–1.4× faster than Dapper, 2–8× faster than EF Core
- **Writes:** Competitive with Dapper, 5–7× faster than EF Core
- **Memory:** Consistently lowest allocations across all benchmarks

## Project Structure

```
src/
  cslightdbgen.sqlitegen/             # Roslyn incremental source generator
tests/
  cslightdbgen.sqlitegen.tests/       # Unit tests (generator contract verification)
  cslightdbgen.sqlitegen.integration/ # Integration tests (runtime SQLite behavior)
  cslightdbgen.performance/           # BenchmarkDotNet comparisons
docs/                                 # Developer documentation
```

## Building & Testing

```powershell
# Build
dotnet build db-gen.slnx -c Release

# Unit tests
dotnet test tests/cslightdbgen.sqlitegen.tests -c Release

# Integration tests
dotnet test tests/cslightdbgen.sqlitegen.integration -c Release

# Benchmarks
dotnet run -c Release --project tests/cslightdbgen.performance
```

## Documentation

Detailed developer docs are in the [docs/](docs/) directory:

- [Onboarding Guide](docs/onboarding.md)
- [Architecture Overview](docs/architecture.md)
- [Process Flows](docs/process-flows.md)
- [Generated API Contract](docs/api-contracts.md)
- [Commands and Options](docs/commands.md)
- [Dependencies](docs/dependencies.md)

## Technical Details

- Generator targets **netstandard2.0** for maximum IDE/SDK compatibility
- Uses Roslyn **Microsoft.CodeAnalysis.CSharp 4.14.0**
- Generated code uses only ADO.NET abstractions (`IDbConnection`, `IDbCommand`, `IDataReader`)
- No runtime reflection for CRUD operations
- Parameterized SQL for all values
- C# 14.0 language features

## License

[MIT](LICENSE) — Copyright (c) Gino Canessa
