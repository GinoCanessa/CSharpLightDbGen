# CSharpLightDbGen

[![Tests](https://github.com/GinoCanessa/CSharpLightDbGen/actions/workflows/build-and-test.yaml/badge.svg)](https://github.com/GinoCanessa/CSharpLightDbGen/actions/workflows/build-and-test.yaml)
[![Publish dotnet generator](https://img.shields.io/nuget/v/cslightdbgen.sqlitegen.svg)](https://github.com/GinoCanessa/CSharpLightDbGen/actions/workflows/nuget-generator.yaml)

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
| `LdgSQLiteKey(autoIncrement)` | Property | Designates a primary key column. Applying it to two or more properties declares a **composite** primary key (emitted as a table-level `PRIMARY KEY (...)` constraint). |
| `LdgSQLiteForeignKey(refTable?, refColumn?, modelTypeName?, onDelete?, onUpdate?)` | Property | Declares a single-column foreign key. `onDelete`/`onUpdate` accept `LdgSQLiteFkAction` (`NoAction`, `Restrict`, `SetNull`, `SetDefault`, `Cascade`) and emit `ON DELETE`/`ON UPDATE` clauses when non-default. |
| `LdgSQLiteForeignKeyComposite(columns, refTable, refColumns, onDelete?, onUpdate?)` | Class | Declares a multi-column (composite) foreign key. `AllowMultiple = true`. Same `LdgSQLiteFkAction` referential actions as `LdgSQLiteForeignKey`. |
| `LdgSQLiteIndex(columns..., Unique?, Where?)` | Class | Emits a composite index on the specified columns. Set `Unique = true` for a `CREATE UNIQUE INDEX`; set `Where = "<predicate>"` for a partial index (`... WHERE <predicate>`). Unique/partial indexes get a deterministic name suffix so migrations stay idempotent. |
| `LdgSQLiteUnique(columns...)` | Property or Class | On a property, adds an inline `UNIQUE` column constraint. On a class, declares a multi-column `UNIQUE (...)` table constraint (`AllowMultiple = true`). |
| `LdgSQLiteDefault(value?, raw?)` | Property | Emits a column `DEFAULT` clause. String values are SQL-quoted (embedded `'` doubled); `bool` maps to `1`/`0`; numeric values render as-is. Set `raw: true` to emit an unquoted SQL expression such as `CURRENT_TIMESTAMP`. |
| `LdgSQLiteMultiSelect` | Property | Emits `IEnumerable<T>? {Name}Values` (SQL `IN (...)`) and `IEnumerable<T>? {Name}NotInValues` (SQL `NOT IN (...)`) parameters on filter/delete helpers. Implicitly applied to `[LdgSQLiteKey]` primary keys and to any non-array property whose name ends in `Key`. Non-scalar / JSON / collection properties are not eligible. |
| `LdgSQLiteIgnore` | Property | Excludes the property from generation |
| `LdgSQLiteFtsTable(sourceTable, tableName?)` | Class | Generates an FTS5 full-text search table |
| `LdgSQLiteFtsUnindexed` | Property | Marks an FTS column as `UNINDEXED` |

## Generated API Surface

### Standard Table (`[LdgSQLiteTable]`)

**Schema:** `CreateTable`, `DropTable`, `EnsureSchema`

> **Additive migration (`EnsureSchema`):** `EnsureSchema(dbConnection, dbTableName?, transaction?)` brings an existing table up to the current model without dropping data: it runs `CREATE TABLE IF NOT EXISTS`, reads `PRAGMA table_info`, adds any missing columns with `ALTER TABLE … ADD COLUMN`, and (re)creates missing indexes (`IF NOT EXISTS`). Because `CREATE TABLE IF NOT EXISTS` is a no-op on a pre-existing table, any class-level composite `[LdgSQLiteUnique(cols)]` (a table-level `UNIQUE` constraint that a plain re-run cannot restore) is additionally re-asserted as an equivalent `CREATE UNIQUE INDEX IF NOT EXISTS` so it is enforced after migration. It is **additive only** — it never drops columns, changes column types, or backfills beyond SQLite's own `ADD COLUMN` default fill. Added columns cannot carry `PRIMARY KEY`/`UNIQUE` constraints or non-constant defaults; a `NOT NULL` model column lacking a constant default is added as nullable (SQLite forbids adding a `NOT NULL` column with no default to a populated table). Running it repeatedly is idempotent. Available as an extension too: `dbConnection.EnsureSchema<MyModel>()`.

> **Foreign-key enforcement:** SQLite requires `PRAGMA foreign_keys = ON` **per connection** for foreign-key constraints (including `ON DELETE`/`ON UPDATE` actions) to be enforced. `CreateTable` does **not** emit this PRAGMA — it is connection-scoped and therefore the caller's responsibility. Enable it on each connection before relying on referential actions.

**Reads:** `SelectSingle`, `SelectList`, `SelectEnumerable`, `SelectDict`, `SelectCount`
- Filter by any property via named parameters
- Numeric operator filters (`Age`, `AgeOperator: ">="`)
- Nullable tri-state filters (`ScoreIsNull: true`)
- `IN` / `NOT IN` list filters (`SegmentKeyValues: [1, 2, 3]`, `StatusNotInValues: ["complete", "failed"]`) — generated for any property marked `[LdgSQLiteMultiSelect]`, the primary key, or (legacy heuristic) a name ending in `Key`
- `LIKE` matching for strings (`compareStringsWithLike: true`)
- Paging (`resultLimit`, `resultOffset`) and ordering (`orderByProperties`, `orderByDirection`, `orderByDirections`)
- Multi-column ordering with per-column direction: pass `orderByProperties: ["Age", "Name"]` together with `orderByDirections: ["desc", "asc"]` to emit `ORDER BY Age DESC, Name ASC` (each entry `d*` = descending, anything else = ascending). `orderByDirection` remains the single fallback direction, applied to any column without a matching `orderByDirections` entry (including when `orderByDirections` is shorter than `orderByProperties` or omitted)
- Unknown `orderByProperties` values are ignored; directions stay paired to the surviving columns by their original position, so an invalid column in the middle of the list does not shift later columns onto the wrong direction. Generated models expose `SQLiteColumnNames` for the valid SQLite column names
- Optional trailing `transaction` (`IDbTransaction?`) enrolls the read in a caller-supplied transaction. When omitted, the read still honors any ambient transaction already open on the connection; pass the transaction explicitly for deterministic composition.

**Writes:** `Insert`, `InsertReturning`, `Update`, `UpdateReturning`, `Upsert`, `Delete` — single value, list, or enumerable overloads
- `ignoreDuplicates` → `INSERT OR IGNORE`
- `insertPrimaryKey` → include PK in insert
- List/enumerable `Insert` overloads surface each element's generated identity key back onto the passed instances (single round-trip per row)
- Optional trailing `transaction` (`IDbTransaction?`) — when supplied, the write enrolls in the caller's transaction and leaves commit/rollback to the caller so multiple calls compose atomically. When omitted, each write opens and commits its own transaction (auto-commit, unchanged behavior).

> **RETURNING hydration (`InsertReturning` / `UpdateReturning`):** `InsertReturning(dbConnection, value, dbTableName?, ignoreDuplicates?, insertPrimaryKey?, transaction?)` and `UpdateReturning(dbConnection, value, dbTableName?, transaction?)` issue the write with a trailing `RETURNING <all columns>` (requires SQLite ≥ 3.35) and return a nullable `T?`: when a row is produced they hydrate the passed `value` **in place** from the returned row and return that same instance; when **no row** is produced — an `UpdateReturning` whose `WHERE` matched nothing, or an `InsertReturning(ignoreDuplicates: true)` whose row was ignored on conflict — they return `null`. This makes server-computed values visible in one round-trip — most importantly the generated identity key and any raw/expression column default (e.g. `CreatedAt = CURRENT_TIMESTAMP`). Because SQLite only applies a column `DEFAULT` when the column is omitted from the `INSERT`, `InsertReturning` **omits** raw/expression-default columns from the `INSERT` (so the database computes them) and then reads the computed value back via `RETURNING`; constant defaults are still bound from the model like a normal `Insert`. Composes with a caller-supplied `transaction` like the other writes.

> **Upsert (`INSERT … ON CONFLICT`):** `Upsert(dbConnection, value, conflictColumns?, updateColumns?, incrementColumns?, dbTableName?, transaction?)` issues a single SQLite `INSERT … ON CONFLICT(<target>) DO UPDATE/NOTHING` (requires SQLite ≥ 3.24) and returns `void`. `conflictColumns` names the conflict target; it defaults to the model's key columns for a composite or natural (non-identity) primary key, but an **identity** (auto-increment) or keyless model has no natural conflict target, so `conflictColumns` is required and omitting it throws `ArgumentException`. `updateColumns` defaults to every non-identity column except the conflict target; passing an empty array turns the statement into `DO NOTHING` (existing row untouched, may affect zero rows). `incrementColumns` (a subset of `updateColumns`) accumulates instead of overwriting — `col = col + excluded.col`. Composes with a caller-supplied `transaction` like the other writes.

**Utilities:** `LoadMaxKey`, `SelectMaxKey`, `SQLiteColumnNames`

**Extensions:** convenience methods on `IDbConnection`, model instances, and collections

> **Composite primary keys:** declaring two or more `[LdgSQLiteKey]` properties emits a table-level `PRIMARY KEY (col, ...)` constraint (each key column is `NOT NULL`) instead of an inline single-column directive. Composite-key columns are never auto-incremented, so `Insert` returns `void`, includes every key column in the `INSERT`, and emits no `RETURNING` clause — supply all key values before inserting. `Update`/`Delete`-by-key match on the full key (`col1 = $col1 AND col2 = $col2`). `SelectDict` (which keys the result by a single primary key) is omitted for composite-key tables.

### FTS Table (`[LdgSQLiteFtsTable]`)

`CreateTable`, `DropTable`, `Populate`, `Select` (via `MATCH`), `SelectCount`
- Optional HTML stripping during `Populate(..., sanitizeText: true)`
- Generated FTS models also expose `SQLiteColumnNames`, and invalid `orderByProperties` values are ignored. FTS `Select` supports the same multi-column `orderByDirections` (per-column direction) as regular reads
- `Populate`, `Select`, and `SelectCount` accept the same optional trailing `transaction` (`IDbTransaction?`) for transactional composition

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
- Uses Roslyn **Microsoft.CodeAnalysis.CSharp 5.3.0**
- Generated code uses only ADO.NET abstractions (`IDbConnection`, `IDbCommand`, `IDbTransaction`, `IDataReader`)
- No runtime reflection for CRUD operations
- Parameterized SQL for all values
- C# 14.0 language features
- **SQLite version:** all generated APIs run on modern SQLite. Two write helpers use newer syntax — `Upsert` (`INSERT … ON CONFLICT`) requires **SQLite ≥ 3.24**, and `InsertReturning`/`UpdateReturning` (`RETURNING`) require **SQLite ≥ 3.35**. Foreign-key referential actions require `PRAGMA foreign_keys = ON` per connection (see the note above).

## License

[MIT](LICENSE) — Copyright (c) Gino Canessa
