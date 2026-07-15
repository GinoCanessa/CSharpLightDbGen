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
- `LdgSQLiteMultiSelect`
- `LdgSQLiteFtsTable`
- `LdgSQLiteFtsUnindexed`

These attribute types are emitted as **`public`** (not `internal`) so that models and generated APIs
remain usable across assembly boundaries in a multi-project solution (see [Multi-Project Placement](#4-multi-project-placement-cs0436)).

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

### 4) Multi-Project Placement (CS0436)

Attribute definitions are added via `RegisterPostInitializationOutput`, so **every project that runs
the analyzer** gets its own copy of the (public) attribute types. Because the types are public and the
generated DAL is public and `static`, the supported multi-project model is:

- **Host the analyzer in exactly one project.** Other projects take an ordinary `ProjectReference`
  (without `OutputItemType="Analyzer"`) to that project and consume the generated public DAL and
  public attribute types as metadata. The attribute types are declared once, so there is no
  duplicate-type conflict.
- **If two mutually-referencing projects both host the analyzer**, the downstream project sees the
  attribute types both in its own source-generated output and in the referenced assembly's metadata.
  The compiler reports **`CS0436`** ("type conflicts with the imported type") — a **warning**, not an
  error; the build still succeeds using the locally-generated copy. The recommended fix is the
  single-host topology above; alternatively the consuming project can set
  `<NoWarn>$(NoWarn);CS0436</NoWarn>`.

The attributes are intentionally **not** internalized: internal attribute types would break the
first (recommended) topology, where downstream projects reference the generated types across the
assembly boundary.

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
- **Auto-increment identity keys use a process-wide `static` counter (`_indexValue`) per generated
  model.** `GetIndex()` (`Interlocked.Increment`) hands out the next integer key, and
  `LoadMaxKey` — invoked by `EnsureSchema`/`CreateTable` for identity models — seeds it from
  `SELECT MAX(key)` on a single connection. Because the counter is shared across every
  `IDbConnection` used with that model in the process, the generated identity assignment assumes
  **one logical database per model type per process**. Using the same model against multiple
  databases concurrently (or mutating the table out-of-band) can desynchronize the counter; re-seed
  it with `LoadMaxKey` against the target connection, or supply keys explicitly. Natural-key
  (`string`/`Guid`), composite-key, and keyless models are inserted explicitly, never auto-assigned,
  and so are unaffected.
