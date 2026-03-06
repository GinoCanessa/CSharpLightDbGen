# Architecture Overview

## Purpose

`CSharpLightDbGen` is a Roslyn incremental source generator that emits strongly-typed SQLite data access APIs for attributed models.

The generated code targets runtime `IDbConnection` usage and avoids runtime reflection-heavy ORM patterns for core CRUD operations.

## Solution Structure

```text
src/
  cslightdbgen.sqlitegen/             # Source generator (Roslyn analyzer component)
tests/
  cslightdbgen.sqlitegen.tests/       # Unit-level generator contract tests
  cslightdbgen.sqlitegen.integration/ # End-to-end runtime SQLite behavior tests
  cslightdbgen.performance/           # BenchmarkDotNet performance comparisons
```

## Core Components

### 1) Generator Attributes (`GeneratorAttributes.cs`)

Provides attribute source text injected into consuming compilations:

- `LdgSQLiteBaseClass`
- `LdgSQLiteTable`
- `LdgSQLiteIndex`
- `LdgSQLiteKey`
- `LdgSQLiteForeignKey`
- `LdgSQLiteIgnore`
- `LdgSQLiteUnique`
- `LdgSQLiteFtsTable`
- `LdgSQLiteFtsUnindexed`

### 2) Incremental Generator (`LightSQLiteGenerator.cs`)

Implements `IIncrementalGenerator` and does the following:

1. Emits attribute definitions in post-initialization.
2. Discovers target class/record declarations from syntax attributes.
3. Builds semantic metadata (columns, key, nullability, JSON handling, inheritance).
4. Emits model-specific generated partials and extension wrappers.

### 3) Generated Runtime Surface

For regular table models (`[LdgSQLiteTable]`):

- DDL: create/drop table, index creation
- Queries: `SelectSingle`, `SelectList`, `SelectEnumerable`, `SelectDict`, `SelectCount`
- Mutations: `Insert`, `Update`, `Delete`
- Helpers: max-key loading, numeric operator mapping, JSON serialization/parsing helpers
- Extensions: convenience overloads on `IDbConnection`, model instances, and collections

For FTS models (`[LdgSQLiteFtsTable]`):

- DDL: `CREATE VIRTUAL TABLE ... using fts5`
- Population from source table (`Populate`)
- Search by term list (`MATCH`) and count
- Optional text sanitization with HTML stripping

## Runtime Boundary

The generator project only depends on Roslyn packages. Runtime DB behavior is in emitted code and uses ADO.NET abstractions (`IDbConnection`, `IDbCommand`, `IDataReader`).

This keeps the generator package lean and lets consuming applications choose their provider (`Microsoft.Data.Sqlite` in tests/benchmarks).

## Design Characteristics

- Compile-time code generation (no runtime schema discovery).
- Per-model static APIs for predictable invocation.
- Parameterized SQL for values.
- Optional string `LIKE` matching and numeric operator controls for filters.
- Optional `IN` list filters for `*Key` properties.
- Support for classes and `record class` models.

## Known Architectural Tradeoffs

- Attribute detection is name-string based; naming/qualification conventions matter.
- `orderByProperties` and table names are interpolated SQL fragments; they should be treated as trusted internal inputs.
- Base-member discovery is type-name based in generator logic, which can be ambiguous in duplicate-name namespace scenarios.
