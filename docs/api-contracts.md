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
  - Marks a primary key column. A single `[LdgSQLiteKey]` on an `int`/`long` property is emitted as an inline auto-increment identity directive; other single-key types are inline `PRIMARY KEY` columns.
  - Declaring `[LdgSQLiteKey]` on two or more properties forms a **composite** primary key, emitted as a table-level `PRIMARY KEY (col1, col2, ...)` constraint. Composite key columns are non-identity and `NOT NULL`; they receive no inline PK directive.
  - Composite-key behavioral differences: `Insert` returns `void`, includes every key column in the `INSERT` column list (values must be supplied by the caller), and emits no `RETURNING` clause. `Update`/`Delete`-by-key match on the full key (`col1 = $col1 AND col2 = $col2`). `SelectDict` is not generated (a dictionary keyed by a single primary key is undefined for composite keys).
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
- `InsertReturning(IDbConnection dbConnection, T value, string? dbTableName = null, bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)` → returns the same `T` instance, hydrated from `RETURNING`
- `Update(IDbConnection dbConnection, T value, string? dbTableName = null, IDbTransaction? transaction = null)`
- `UpdateReturning(IDbConnection dbConnection, T value, string? dbTableName = null, IDbTransaction? transaction = null)` → returns the same `T` instance, hydrated from `RETURNING`
- `Update(IDbConnection dbConnection, IEnumerable<T> values, string? dbTableName = null, IDbTransaction? transaction = null)`
- `Delete(IDbConnection dbConnection, T value, string? dbTableName = null, IDbTransaction? transaction = null)`
- `Delete(IDbConnection dbConnection, IEnumerable<T> values, string? dbTableName = null, IDbTransaction? transaction = null)`
- `Delete(IDbConnection dbConnection, ..., [filter params], IDbTransaction? transaction = null)`
- `Upsert(IDbConnection dbConnection, T value, string[]? conflictColumns = null, string[]? updateColumns = null, string[]? incrementColumns = null, string? dbTableName = null, IDbTransaction? transaction = null)`

### Insert Options

- `ignoreDuplicates`: emits `INSERT OR IGNORE`.
- `insertPrimaryKey`: includes PK in insert statement instead of identity behavior.
- List/enumerable `Insert` overloads surface each element's generated identity key back onto the passed instances.

### RETURNING / Hydration Options

`InsertReturning` and `UpdateReturning` append a trailing `RETURNING <all columns>` clause
(requires SQLite ≥ 3.35), read the single returned row, hydrate the passed `value` **in place**,
and return that same instance. Use them to observe server-computed values in one round-trip:

- The generated identity key and any raw/expression column default (e.g. `CreatedAt = CURRENT_TIMESTAMP`) become visible on the returned instance.
- `InsertReturning` **omits** raw/expression-default columns from the `INSERT` so SQLite computes them, then reads them back via `RETURNING`. Constant defaults are still bound from the model (identical to `Insert`). This is the one behavioral divergence from `Insert`, which binds every non-identity column.
- `UpdateReturning` sets every non-PK column (identical `SET` list to `Update`) and hydrates all columns from the updated row.

### Upsert Options

`Upsert` issues a single SQLite `INSERT … ON CONFLICT(<target>) DO UPDATE/NOTHING` (requires SQLite ≥ 3.24) and returns `void`.

- `conflictColumns`: the conflict target. Defaults to the model's key columns for a composite or natural (non-identity) primary key. An **identity** (auto-increment) or keyless model has no natural conflict target, so this argument is required — omitting it throws `ArgumentException`.
- `updateColumns`: columns assigned on conflict. Defaults to every non-identity column except the conflict target. Passing an empty array (`[]`) turns the statement into `DO NOTHING` (existing row untouched; may affect zero rows).
- `incrementColumns`: a subset of `updateColumns` that accumulates rather than overwrites — `col = col + excluded.col`.

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
