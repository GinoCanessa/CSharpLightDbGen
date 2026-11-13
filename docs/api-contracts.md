# Generated API Contract

This document summarizes the generated API surface developers should expect for attributed models.

## SQL Identifier Handling

- **Column identifiers are always quoted.** Every column name the generator emits into DDL and
  DML (`CREATE TABLE`, `INSERT`/`UPDATE`/`DELETE`, `SELECT` lists, `WHERE`/`IN` predicates,
  `ORDER BY`, `RETURNING`, `ON CONFLICT`, and FTS column declarations) is wrapped in SQLite
  double-quotes. This means columns whose names are SQL reserved words (e.g. `Order`, `Group`,
  `Table`) round-trip correctly, and runtime-supplied identifiers (`orderByProperties`, `Upsert`
  `conflictColumns`/`updateColumns`/`incrementColumns`) are validated against the model's known
  column set and rejected with `ArgumentException` if unknown, so they cannot inject SQL.
- **Table names are trusted identifiers.** The table name — whether the compile-time
  `LdgSQLiteTable` name or a runtime `dbTableName` override (including `dynamicTableNames`) — is
  inlined into SQL **unquoted and unvalidated**. Callers must supply only trusted table names;
  the generator does not defend against a hostile `dbTableName`.

## Attribute Contract

### Class/Record Level

- `LdgSQLiteBaseClass`
  - Marks a base model participating in inherited member collection.
- `LdgSQLiteTable(string? tableName = null, bool dynamicTableNames = false)`
  - Generates regular table CRUD/query API.
- `LdgSQLiteFtsTable(string sourceTable, string? tableName = null)`
  - Generates FTS5 table/search API.
- `LdgSQLiteJsonOptions(Type factoryType, string factoryMethod)`
  - Single-use, direct, noninherited class/record-class selection for table or FTS
    JSON persistence. Exposes read-only `Type FactoryType` and `string FactoryMethod`.
    See [JSON persistence options](#json-persistence-options).
- `LdgSQLiteIndex(params string[] columns)` with named args `bool Unique = false`, `string? Where = null`
  - Emits index creation SQL for the listed columns.
  - `Unique = true` emits `CREATE UNIQUE INDEX` instead of `CREATE INDEX`.
  - `Where = "<predicate>"` emits a partial index (`... WHERE <predicate>`). The predicate is inlined verbatim into the DDL.
  - Unique and/or partial indexes receive a deterministic (FNV-1a) name suffix so repeated schema application does not create duplicate indexes.
- `LdgSQLiteUnique(params string[] columns)` *(class-level use)*
  - Declares a multi-column `UNIQUE (col1, col2, ...)` table constraint. `AllowMultiple = true`, so a model may declare several composite unique constraints.
- `LdgSQLiteForeignKeyComposite(string[] columns, string referenceTable, string[] referenceColumns, LdgSQLiteFkAction onDelete = NoAction, LdgSQLiteFkAction onUpdate = NoAction)`
  - Emits a multi-column (composite) `FOREIGN KEY (...) REFERENCES <table> (...)` table constraint. `AllowMultiple = true`, so a model may declare several composite foreign keys.
  - `onDelete`/`onUpdate` use the same `LdgSQLiteFkAction` referential actions as `LdgSQLiteForeignKey`.
  - > Foreign-key constraints (single or composite) are only enforced when the caller enables `PRAGMA foreign_keys = ON` on the connection. `CreateTable` does not emit this connection-scoped PRAGMA.

### Property Level

- `LdgSQLiteKey(bool autoIncrement = true)`
  - Marks a primary key column; exposes settable `AutoIncrement`. A single key on an `int`/`long` property with `AutoIncrement = true` uses the existing generated identity behavior; other single keys are non-identity.
  - Declaring keys on two or more properties forms a **composite** primary key, emitted as a table-level `PRIMARY KEY (col1, col2, ...)` constraint. Composite members are non-identity; old-style members emit `NOT NULL` only through their existing CLR-nullability branch, not for nullable members.
  - Composite-key behavioral differences: `Insert` returns `void`, includes every key column in the `INSERT` column list (values must be supplied by the caller), and emits no `RETURNING` clause. `Update`/`Delete`-by-key match on the full key (`col1 = $col1 AND col2 = $col2`). `SelectDict` is not generated (a dictionary keyed by a single primary key is undefined for composite keys).
- `LdgSQLiteKeyNoUnique(bool autoIncrement = true)`
  - Independent property annotation with settable `bool AutoIncrement`, not a
    subclass of `LdgSQLiteKey`. Same logical keys, without injected `UNIQUE` or
    `NOT NULL` (including CLR-nullability-derived constraints).
- `LdgSQLiteForeignKey(string? referenceTable = null, string? referenceColumn = null, string? modelTypeName = null, LdgSQLiteFkAction onDelete = NoAction, LdgSQLiteFkAction onUpdate = NoAction)`
  - Single-column foreign key. `onDelete`/`onUpdate` emit `ON DELETE <action>` / `ON UPDATE <action>` clauses in the `CREATE TABLE` DDL when set to a non-`NoAction` value.
  - `LdgSQLiteFkAction` members map to SQLite referential actions: `NoAction` → `NO ACTION` (omitted), `Restrict` → `RESTRICT`, `SetNull` → `SET NULL`, `SetDefault` → `SET DEFAULT`, `Cascade` → `CASCADE`.
- `LdgSQLiteIgnore()`
- `LdgSQLiteUnique(params string[] columns)` *(property-level use)*
  - Adds an inline `UNIQUE` constraint to the column. Column arguments are ignored for property-level use; the same attribute is dual-targeted so it may also be applied at the class level for composite unique constraints (see Class Level).
- `LdgSQLiteDefault(object? value = null, bool raw = false)`
  - Emits a `DEFAULT` clause in the generated `CREATE TABLE` DDL for the column.
  - `string` values are rendered as SQL string literals with embedded `'` doubled (e.g. `DEFAULT 'queued'`).
  - `bool` values render as `1`/`0`; numeric and other literals render verbatim (e.g. `DEFAULT 0`).
  - `raw: true` emits the string value as an unquoted SQL expression (e.g. `LdgSQLiteDefault("CURRENT_TIMESTAMP", raw: true)` → `DEFAULT CURRENT_TIMESTAMP`).
  - A `null` (or omitted) value emits no clause. Not applicable to `[LdgSQLiteFtsTable]` models (FTS5 columns cannot carry defaults).
- `LdgSQLiteMultiSelect()`
- `LdgSQLiteFtsUnindexed()`

### Assembly Level / Multi-Host Ownership

`LdgSQLiteRuntimeNamespace(string value)` is assembly-targeted, single-use and
noninherited, with read-only `string Value`. Default mode retains public annotations,
public `LdgSQLiteFkAction`, and public sealed
`CsLightDbGen.SQLiteGenerator.LdgCommandFailedException`.

Each local host must append the **project-wide** symbol:

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);CSLIGHTDBGEN_MULTI_HOST</DefineConstants>
</PropertyGroup>
```

and provide its own assembly annotation:

```csharp
// A's source; B uses "CsLightDbGen.Integration.HostB.Runtime" instead.
[assembly: CsLightDbGen.SQLiteGenerator.LdgSQLiteRuntimeNamespace("CsLightDbGen.Integration.HostA.Runtime")]
```

A file-local `#define` is not sufficient. In this mode every top-level annotation and
the FK enum are internal; annotation constructors/properties remain public members.
The helper is still public sealed `LdgCommandFailedException`, in the chosen namespace,
with **no default-name alias**. Its base `System.Exception`, constructor
`(string modelName, string sql)`, read-only `ModelName`/`Sql`, guards, and message remain:

```text
The command for model '{modelName}' affected no rows (expected at least one). SQL: {sql}
```

The helper is emitted even for a host with no models. Namespace/type-name changes
require consumer rebuilds and explicit catch/type-reference review, not an assumption
of binary compatibility.

Supported composition: A and B both local; B ordinarily references A without friend
access; C ordinarily references both, calls both DALs and catches both exception types.
Model-specific extensions remain public in model namespaces. Internal annotation
metadata on referenced bases, keys and tables remains discoverable, including for FTS.
C may be non-analyzer or the tested third, default-mode integration host. No runtime
generator reference, shared runtime package, consolidation, `extern alias` or warning
suppression is needed.

Default two-host duplicate-public-type warning behavior remains intentional. A single
default host remains the simple public-annotation topology. Mixed default/local
annotation hosts and `InternalsVisibleTo` exposure are not guaranteed warning-free;
the tested C topology imports only local hosts' internal annotations.

### Configuration Diagnostics

- **CSLDG007**: invalid model-local JSON factory binding; reported at its annotation
  (model location fallback). Rejects only that model, not unrelated models.
- **CSLDG008**: invalid host configuration; reported at the assembly annotation
  (`Location.None` if missing). Requires both symbol and annotation in local mode;
  rejects an annotation without the symbol. Namespace must be nonempty dot-separated
  ordinary C# identifiers; keywords, escaped identifiers, empty segments, `global::`
  and source fragments are invalid. Local mode cannot select the default namespace.
  The chosen exception metadata name must not collide in current source or any
  referenced assembly (checked individually, including ambiguous referenced names).
  Invalid hosts keep bootstrap annotations but suppress ordinary helpers/table/FTS output.

Both rules remain in the Unshipped analyzer ledger; this documentation does not
announce a package release.

## Primary-Key Constraint Policy

| Shape | Generated constraints |
|---|---|
| Old single `[LdgSQLiteKey]` | Existing `UNIQUE PRIMARY KEY NOT NULL` and explicit UNIQUE/default ordering unchanged |
| New single `[LdgSQLiteKeyNoUnique]` | Inline `PRIMARY KEY`; no injected UNIQUE/NOT NULL, even for CLR-nonnullable properties |
| Old composite member | No inline PK; retain existing CLR-nullability-derived NOT NULL only |
| New composite member | No inline PK and no injected UNIQUE/NOT NULL, regardless of CLR nullability |
| All-new or mixed composite | One ordered table-level PRIMARY KEY using **all** old/new members; never identity |

Explicit property/class `LdgSQLiteUnique` constraints and unique indexes remain
independently effective. Both key annotations on the **same** property report
`CSLDG005` and reject the model; different members may mix styles. An explicit
composite auto-increment request reports `CSLDG003`.

For the new annotation, effective constructor/named `AutoIncrement` values are honored,
including `false`. Source argument explicitness is resolved from the actual attribute
syntax (including aliases/qualification). In metadata, bare usage and explicit
default-valued constructor `true` are indistinguishable: default constructor explicitness
is treated as unknown/non-explicit for `CSLDG003`. Constructor `false` stays false;
named `AutoIncrement = true` remains an observable explicit request.

This removes extra decoration, **not primary-key uniqueness**. SQLite INTEGER PRIMARY
KEY retains rowid behavior. Text/composite keys without NOT NULL can admit raw SQL NULL;
there is no new NULL-safe CLR reader, key predicate or dictionary support. Use raw SQL
to assess those nullable storage constraints rather than assume nonnullable model APIs
can hydrate arbitrary NULL keys.

Logical key membership still drives full-key mutation predicates, key filters,
`SelectDict` eligibility, default Upsert conflict targets and schema key exclusion.
Single integer keys with auto-increment enabled retain existing counters, insert
exclusions and hydration (including list/enumerable writes); `AutoIncrement = false`
uses supplied keys. No identity/counter policy changes are introduced.
`EnsureSchema` is additive: it does not add missing keys, rebuild/reconcile tables,
remove old constraints, or repair key constraints when annotations change.

## JSON Persistence Options

The binding applies to **all supported JSON columns of that model**, including
inherited properties. It is not inherited from a base class or source table:
derived tables and FTS projections must each select a factory explicitly. Multiple
models may reuse one factory; each owns its own options snapshot. Unconfigured
models retain the existing default options/wrappers. No codec or global mutable
policy registry is introduced. A practical standalone options/model example is in
the [README](../README.md#consumer-json-options).

### Factory Validation

Validation inspects symbols; it never executes consumer code. Require:

- A closed accessible named type, including accessible containing types. Closed
  constructed generic factory types are accepted.
- Exactly one **declared**, ordinary static, nongeneric, zero-parameter method with
  the exact supplied name and the compilation's `System.Text.Json.JsonSerializerOptions`
  return-type identity. Parameterized overloads are harmless; an optional parameter
  still counts as a parameter. An inherited method alone does not qualify.
- Accessibility from the separate generated partial source file: public or
  assembly-accessible internal members, including referenced factories when accessible.

Reject open generics, file-local/inaccessible types, private/protected-only members,
missing/ambiguous methods, wrong returns, explicit interface implementations and
interface-only static abstract/virtual members that cannot be directly invoked.
Calls use fully qualified resolved type names and escaped method identifiers, never
unchecked source fragments. Nullable return annotations do not bypass the runtime
null check. Invalid bindings report CSLDG007 even for a scalar-only model; a valid
unused binding emits no JSON helper and never invokes the factory.

### First Use / Failures / Ownership

Configured models emit one private `Lazy<JsonSerializerOptions>` with
`LazyThreadSafetyMode.ExecutionAndPublication`. Its initializer directly invokes
the factory, rejects null with an `InvalidOperationException` naming model/factory,
then copies `new JsonSerializerOptions(returnedOptions)`. All four serialization/
parsing wrappers use the same lazy value. Initialization occurs once per generated
model on **real JSON serialization/deserialization**, not model static initialization.
Schema, metadata, counters, SQL NULL reads, and null/empty/whitespace early-return
guards do not execute the factory.

The shallow snapshot is independent of later changes to the consumer-owned original;
the snapshot becomes read-only on serializer use, not the original. Converter instances
are shared, not cloned: use thread-safe converters and do not concurrently mutate the
returned options during copying. Lazy initialization exceptions are cached, visible
on subsequent real JSON use, and never retried or replaced with defaults. The model
type initializer is not poisoned, so schema and guard-only operations remain usable.
Factory, converter and malformed-JSON failures propagate; converter/invalid-input
failures during ordinary serialization are not described as cached initialization.

### Null / Empty / Storage Matrix

These are existing wrapper semantics, not normalization performed by a new codec.

| Input/path | Result |
|---|---|
| Null object write | Wrapper returns no JSON; bind SQL NULL for nullable column, empty text for nonnullable |
| Null or empty `List<T>` write | Same no-JSON sentinel; no factory execution |
| Null array/enumerable write | Generic class wrapper's null guard; same SQL NULL/empty-text binding |
| Non-null empty array/enumerable write | Generic serializer path, normally `[]`; factory executes (a runtime `List<T>` passed as enumerable does not select the List overload) |
| Nullable SQL NULL read | Return null via `IsDBNull`, without parsing/factory |
| Nonnullable SQL NULL read | Unchanged nonnullable reader path; no promise to accept raw SQL NULL |
| Empty/whitespace object text read | Return null without parsing/factory; nonnullable object reader supplies `new T()` |
| Empty/whitespace collection text read | Empty list; arrays use `ToArray()`; no factory |
| JSON `null` object text read | Deserializer runs; if its result is null, keep null or supply `new T()` for nonnullable object |
| JSON `null` collection text read | Deserializer runs; if its result is null, use empty list/array even for nullable collection (text is not SQL NULL) |

Null equality filter arguments omit the condition; use nullable `PropertyIsNull:
true/false` for explicit `IS NULL`/`IS NOT NULL`. A non-null empty List equality
filter still binds the no-JSON sentinel (SQL NULL if nullable, empty text otherwise),
not `IS NULL`. JSON `null` text is not SQL NULL.

Serialization preserves existing overload selection: objects, arrays and
enumerables use the generic serializer wrapper, `List<T>` uses its list wrapper.
Collection reads deserialize **`List<T>`**, including enumerable properties;
arrays are then materialized with `ToArray()`. Use converters compatible with these
concrete shapes. Element converters work with arrays; symmetric array-specific
collection converter behavior is not promised.

## Standard Table Model APIs (`[LdgSQLiteTable]`)

## Static Properties

- `DefaultTableName`

## Schema Methods

- `CreateTable(IDbConnection dbConnection, string? dbTableName = null)`
- `DropTable(IDbConnection dbConnection, string? dbTableName = null)`
- `EnsureSchema(IDbConnection dbConnection, string? dbTableName = null, IDbTransaction? transaction = null)`
  - Additive migration for an existing database. Runs `CREATE TABLE IF NOT EXISTS`, reads `PRAGMA table_info(<table>)`, adds any missing model columns via `ALTER TABLE <table> ADD COLUMN …`, then re-runs index creation (`CREATE [UNIQUE] INDEX IF NOT EXISTS …`).
  - **Composite `UNIQUE` re-assertion:** a class-level `[LdgSQLiteUnique(cols)]` is emitted by `CreateTable` as a table-level `UNIQUE` constraint, which `CREATE TABLE IF NOT EXISTS` cannot restore on a pre-existing table. `EnsureSchema` therefore also emits an equivalent `CREATE UNIQUE INDEX IF NOT EXISTS "UQ_<table>_<cols>" (…)` for each such constraint so it is enforced after migrating an older table. (`CreateTable` does not emit this index — it relies on the table constraint.)
  - **Additive only:** never drops columns, never changes column types, never backfills data beyond SQLite's own `ADD COLUMN` default fill.
  - Respects SQLite `ADD COLUMN` restrictions: added columns carry no `PRIMARY KEY`/`UNIQUE` constraint and only constant (non-`raw`) defaults; primary-key columns are never added this way.
  - **Hydration-safe fail-fast:** a `NOT NULL` model column with no constant default, or a column whose default is `raw` (database-computed, e.g. `CURRENT_TIMESTAMP`), cannot be added by `ALTER TABLE ADD COLUMN` in a form the generated readers can hydrate — it would leave pre-existing rows `NULL` in a slot the readers treat as non-nullable. When such a column is missing from a **populated** table, `EnsureSchema` throws an `InvalidOperationException` naming the column and the reason (a manual migration is required) instead of reporting success. On an **empty** table (no rows to strand) the column is added and `EnsureSchema` succeeds.
  - Idempotent — every `ALTER` is guarded by a `PRAGMA table_info` membership check, so repeated calls are safe. Threads the optional `transaction` onto the `CREATE`/`PRAGMA`/`ALTER`/index commands.
  - Also exposed as an extension: `dbConnection.EnsureSchema<TModel>(string? dbTableName = null, IDbTransaction? transaction = null)`.

## Key Utilities

- `LoadMaxKey(IDbConnection dbConnection, string? dbTableName = null, int defaultValue = 0)`
- `SelectMaxKey(IDbConnection dbConnection, string? dbTableName = null, int defaultValue = 0)`

## Read Methods

- `SelectSingle(...)`
- `SelectList(...)`
- `SelectEnumerable(...)`
- `SelectDict(...)`
  - Returns a `Dictionary<TKey, TModel>` keyed by the single primary key. **Not generated for composite-key tables.**
- `SelectCount(...)`

### Common Read Options

- `dbTableName`: override table name.
- `orJoinConditions`: `false` = `AND`, `true` = `OR` across filters.
- `compareStringsWithLike`: `false` = `=`, `true` = `LIKE` for string filters.
- `orderByProperties`, `orderByDirection`, `orderByDirections`: ordering controls. `orderByProperties` is the ordered list of columns; unknown columns are dropped. `orderByDirections` is an optional parallel array giving a per-column direction (`d*` = descending, anything else = ascending), matched to `orderByProperties` **by original input index** so dropped unknown columns never shift later columns onto the wrong direction. `orderByDirection` is the single fallback direction (`d*` starts descending), used for any column that has no matching `orderByDirections` entry (including when `orderByDirections` is `null` or shorter than `orderByProperties`). Passed as the trailing optional parameter (`..., IDbTransaction? transaction = null, string[]? orderByDirections = null`) on `SelectList`/`SelectEnumerable` and their extension wrappers.
- `resultLimit`, `resultOffset`: paging controls for list/enumerable methods.
- `transaction`: optional `IDbTransaction?`. Enrolls the read in a caller-supplied transaction. When omitted, no explicit `command.Transaction` is set, so any transaction already open on the connection remains in effect for the read.

## Write Methods

- `Insert(IDbConnection dbConnection, T value, ..., bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)`
- `Insert(IDbConnection dbConnection, List<T> values, ..., bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)`
- `Insert(IDbConnection dbConnection, IEnumerable<T> values, ..., bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)`
- `InsertReturning(IDbConnection dbConnection, T value, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)` → returns the hydrated `T?`, or `null` when no row is produced
- `Update(IDbConnection dbConnection, T value, string? dbTableName = null, IDbTransaction? transaction = null, bool throwOnZeroRowsAffected = true)`
- `UpdateReturning(IDbConnection dbConnection, T value, string? dbTableName = null, IDbTransaction? transaction = null)` → returns the hydrated `T?`, or `null` when no row matched
- `Update(IDbConnection dbConnection, IEnumerable<T> values, string? dbTableName = null, IDbTransaction? transaction = null, bool throwOnZeroRowsAffected = true)`
- `Delete(IDbConnection dbConnection, T value, string? dbTableName = null, IDbTransaction? transaction = null, bool throwOnZeroRowsAffected = true)`
- `Delete(IDbConnection dbConnection, IEnumerable<T> values, string? dbTableName = null, IDbTransaction? transaction = null, bool throwOnZeroRowsAffected = true)`
- `Delete(IDbConnection dbConnection, ..., [filter params], IDbTransaction? transaction = null)`
- `Upsert(IDbConnection dbConnection, T value, string[]? conflictColumns = null, string[]? updateColumns = null, string[]? incrementColumns = null, string? dbTableName = null, IDbTransaction? transaction = null)`

### Insert Options

- `ignoreDuplicates`: emits `INSERT OR IGNORE`.
- `insertPrimaryKey`: includes PK in insert statement instead of identity behavior.
- List/enumerable `Insert` overloads surface each element's generated identity key back onto the passed instances.

### Zero-Rows Behavior (by-key `Update`/`Delete`)

By-key `Update(value)`/`Delete(value)` (single and bulk) enforce that the targeted row exists:

- **Keyed models** throw public `LdgCommandFailedException` (default namespace `CsLightDbGen.SQLiteGenerator`, or the opt-in host's selected runtime namespace; carrying `ModelName` and the offending `Sql`) when a by-key mutation affects **zero** rows — the typical cause is a stale or already-deleted key. Pass `throwOnZeroRowsAffected: false` to opt out **per call** (the mutation then silently affects zero rows). The opt-out is a per-call parameter rather than a process-global toggle so it is thread-safe and localized.
- **Keyless models** (no primary key) can never match a row on a by-key mutation (their predicate is `1 = 0`), so they **never** throw and **omit** the `throwOnZeroRowsAffected` parameter entirely (it would be a permanent no-op).
- `Insert` failure (and FTS `Populate` failure) likewise throws `LdgCommandFailedException` rather than a bare `System.Exception`; the `ignoreDuplicates`/`Upsert` conflict guards are preserved (an ignored conflict does not throw).
- `UpdateReturning` is unaffected: it signals "no row matched" by returning `null`, not by throwing.

### RETURNING / Hydration Options

`InsertReturning` and `UpdateReturning` return a nullable `T?`. They append a trailing
`RETURNING <all columns>` clause (requires SQLite ≥ 3.35); when a row is produced they hydrate the
passed `value` **in place** and return that same instance, and when **no row** is produced they
return `null`. Use them to observe server-computed values in one round-trip:

- The generated identity key and any raw/expression column default (e.g. `CreatedAt = CURRENT_TIMESTAMP`) become visible on the returned instance.
- A `null` return is the explicit "no row affected" signal — `UpdateReturning` whose `WHERE` matched nothing, or `InsertReturning(ignoreDuplicates: true)` whose row was ignored on conflict — and is distinguishable from a successful non-null result.
- `InsertReturning` **omits** raw/expression-default columns from the `INSERT` so SQLite computes them, then reads them back via `RETURNING`. Constant defaults are still bound from the model (identical to `Insert`). This is the one behavioral divergence from `Insert`, which binds every non-identity column.
- `UpdateReturning` sets every non-PK column (identical `SET` list to `Update`) and hydrates all columns from the updated row.

### Upsert Options

`Upsert` issues a single SQLite `INSERT … ON CONFLICT(<target>) DO UPDATE/NOTHING` (requires SQLite ≥ 3.24) and returns `void`.

- `conflictColumns`: the conflict target. Defaults to the model's key columns for a composite or natural (non-identity) primary key. An **identity** (auto-increment) or keyless model has no natural conflict target, so this argument is required — omitting it throws `ArgumentException`.
- `updateColumns`: columns assigned on conflict. When omitted or `null`, resolves to every non-identity column except the conflict target before column validation and SQL construction. Passing an empty array (`[]`) turns the statement into `DO NOTHING` (existing row untouched; may affect zero rows).
- `incrementColumns`: a subset of `updateColumns` that accumulates rather than overwrites — `col = col + excluded.col`.

### Degenerate Model Shapes

Models with no insertable or no updatable columns still generate valid, compilable SQL:

- **Identity-only** (a single auto-increment key, no other columns): the default `Insert` has no columns to supply, so it emits `INSERT INTO <table> DEFAULT VALUES` (not the invalid `() VALUES ()`). Each insert persists a row and auto-assigns the key.
- **Primary-key-only** (a single natural key, no data columns): `Update`/`UpdateReturning` have no assignable columns, so the `SET` clause falls back to a harmless self-assignment of the key column (`"Key" = "Key"`) instead of an empty (invalid) `SET`.
- **Keyless** (no primary key): the by-key `Update`/`Delete(value)` overloads have no row identity, so their `WHERE` predicate is the valid no-op `1 = 0` — they affect zero rows rather than emitting a broken ` = ` predicate. Because zero rows is expected, they **never** throw and omit the `throwOnZeroRowsAffected` opt-out (see *Zero-Rows Behavior*). Use the filter-based `Delete(..., [filter params])` overload to remove keyless rows.

### Transaction Enrollment

Every read and write method — standard and FTS, static and extension-wrapper — accepts an
optional trailing `transaction` parameter (`IDbTransaction? transaction = null`):

- **Writes** (`Insert`/`InsertReturning`/`Update`/`UpdateReturning`/`Upsert`/`Delete`, FTS `Populate`): when a transaction is supplied the
  method enrolls its command(s) in it and leaves `Commit`/`Rollback` to the caller, so multiple
  writes compose atomically. When omitted, each write opens, commits, and disposes its own
  transaction (unchanged auto-commit behavior).
- **Reads** (`SelectSingle`/`SelectList`/`SelectEnumerable`/`SelectDict`/`SelectCount`, FTS
  `Select`/`SelectCount`): enroll only when a transaction is supplied. A read that omits the
  transaction participates in any ambient transaction already open on the connection; pass the
  transaction explicitly for deterministic behavior.

### Execution Model (Synchronous)

Every generated API — standard and FTS, static and extension-wrapper, read and write — is
**synchronous**. The generator emits ADO.NET calls (`IDbCommand.ExecuteNonQuery` /
`ExecuteScalar` / `ExecuteReader`) directly and exposes **no** `async`/`Task`-returning overloads
and **no** `CancellationToken` parameter. Callers that need cancellation or non-blocking I/O should
run the generated call on a background thread (for example `Task.Run(...)`) and/or wrap it with
their own timeout/cancellation policy. `SelectEnumerable` is a lazily-evaluated iterator: it opens
its command and reader on first `MoveNext` and disposes both when the enumerator is disposed
(including on early `break`/abandonment), so it must be enumerated on a single thread within the
lifetime of its connection.

## Generated Filter Parameters (Per Property)

For each mapped property the generator emits one or more filter arguments:

- direct value: `PropertyName`
- numeric comparator: `PropertyNameOperator` (for numeric/date-like primitives)
- nullable tri-state: `PropertyNameIsNull` (`true`/`false`/`null`)
- list `IN` filter: `PropertyNameValues` (`IEnumerable<T>?`) — emits `PropertyName IN (…)`
- list `NOT IN` filter: `PropertyNameNotInValues` (`IEnumerable<T>?`) — emits `PropertyName NOT IN (…)`, binding its own distinct parameters so it can be combined with `PropertyNameValues` on the same column

The `Values`/`NotInValues` pair is generated for any **scalar** property marked `[LdgSQLiteMultiSelect]`, the primary key, or (legacy heuristic) a property whose name ends in `Key`. JSON/array/non-scalar columns never receive them. Both accept any `IEnumerable<T>`; a `null` argument (the default) omits the clause, and an empty sequence is a no-op.

### Supported Numeric Operator Input Aliases

Mapped internally to SQL operators:

- equality: `Equal`, `Equals`, `=`
- inequality: `DoesNotEqual`, `NotEquals`, `!=`
- greater than: `GreaterThan`, `>`
- greater-than-or-equal: `GreaterThanOrEqual`, `GreaterThanOrEquals`, `>=`
- less than: `LessThan`, `<`
- less-than-or-equal: `LessThanOrEqual`, `LessThanOrEquals`, `<=`
- default fallback: `=`

## FTS Model APIs (`[LdgSQLiteFtsTable]`)

### Static Properties

- `DefaultTableName`
- `SourceTableName`

### Methods

- `CreateTable(IDbConnection dbConnection, string? dbTableName = null)`
- `DropTable(IDbConnection dbConnection, string? dbTableName = null)`
- `Populate(IDbConnection dbConnection, string? dbTableName = null, string? sourceTableName = null, bool sanitizeText = false, IDbTransaction? transaction = null)`
- `Select(IDbConnection dbConnection, List<string> matchTerms, string? dbTableName = null, string[]? orderByProperties = null, string? orderByDirection = null, IDbTransaction? transaction = null, string[]? orderByDirections = null)` — supports the same per-column `orderByDirections` semantics as regular reads.
- `SelectCount(IDbConnection dbConnection, List<string> matchTerms, string? dbTableName = null, IDbTransaction? transaction = null)`

### FTS Behaviors

- uses SQLite `fts5` virtual tables,
- maps `[LdgSQLiteFtsUnindexed]` columns to `UNINDEXED`,
- constructs `MATCH` predicates with one parameter per non-empty term,
- optional sanitization path strips HTML prior to insert during `Populate(..., sanitizeText: true)`.

## Extension Wrappers

Generated extension classes mirror static APIs for convenience:

- on `IDbConnection`
- on model instances (`value.Insert(db)` style)
- on model collections (`list.Update(db)` style)

Integration tests demonstrate expected usage patterns for both static and extension entrypoints.
