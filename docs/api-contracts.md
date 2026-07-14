# Generated API Contract

This document summarizes the generated API surface developers should expect for attributed models.

## Attribute Contract

### Class/Record Level

- `LdgSQLiteBaseClass`
  - Marks a base model participating in inherited member collection.
- `LdgSQLiteTable(string? tableName = null, bool dynamicTableNames = false)`
  - Generates regular table CRUD/query API.
- `LdgSQLiteFtsTable(string sourceTable, string? tableName = null)`
  - Generates FTS5 table/search API.
- `LdgSQLiteIndex(params string[] columns)`
  - Emits index creation SQL.
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
- `LdgSQLiteUnique()`
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
- `orderByProperties`, `orderByDirection`: ordering controls (`d*` starts descending).
- `resultLimit`, `resultOffset`: paging controls for list/enumerable methods.
- `transaction`: optional `IDbTransaction?`. Enrolls the read in a caller-supplied transaction. When omitted, no explicit `command.Transaction` is set, so any transaction already open on the connection remains in effect for the read.

## Write Methods

- `Insert(IDbConnection dbConnection, T value, ..., bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)`
- `Insert(IDbConnection dbConnection, List<T> values, ..., bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)`
- `Insert(IDbConnection dbConnection, IEnumerable<T> values, ..., bool ignoreDuplicates = false, bool insertPrimaryKey = false, IDbTransaction? transaction = null)`
- `Update(IDbConnection dbConnection, T value, string? dbTableName = null, IDbTransaction? transaction = null)`
- `Update(IDbConnection dbConnection, IEnumerable<T> values, string? dbTableName = null, IDbTransaction? transaction = null)`
- `Delete(IDbConnection dbConnection, T value, string? dbTableName = null, IDbTransaction? transaction = null)`
- `Delete(IDbConnection dbConnection, IEnumerable<T> values, string? dbTableName = null, IDbTransaction? transaction = null)`
- `Delete(IDbConnection dbConnection, ..., [filter params], IDbTransaction? transaction = null)`

### Insert Options

- `ignoreDuplicates`: emits `INSERT OR IGNORE`.
- `insertPrimaryKey`: includes PK in insert statement instead of identity behavior.

### Transaction Enrollment

Every read and write method — standard and FTS, static and extension-wrapper — accepts an
optional trailing `transaction` parameter (`IDbTransaction? transaction = null`):

- **Writes** (`Insert`/`Update`/`Delete`, FTS `Populate`): when a transaction is supplied the
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
- list `IN` filter: `PropertyNameValues` (generated for non-array properties with names ending in `Key`)

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
- `Select(IDbConnection dbConnection, List<string> matchTerms, string? dbTableName = null, string[]? orderByProperties = null, string? orderByDirection = null, IDbTransaction? transaction = null)`
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
