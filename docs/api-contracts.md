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

### Property Level

- `LdgSQLiteKey(bool autoIncrement = true)`
- `LdgSQLiteForeignKey(string? referenceTable = null, string? referenceColumn = null, string? modelTypeName = null)`
- `LdgSQLiteIgnore()`
- `LdgSQLiteUnique()`
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
- `SelectCount(...)`

### Common Read Options

- `dbTableName`: override table name.
- `orJoinConditions`: `false` = `AND`, `true` = `OR` across filters.
- `compareStringsWithLike`: `false` = `=`, `true` = `LIKE` for string filters.
- `orderByProperties`, `orderByDirection`: ordering controls (`d*` starts descending).
- `resultLimit`, `resultOffset`: paging controls for list/enumerable methods.

## Write Methods

- `Insert(IDbConnection dbConnection, T value, ..., bool ignoreDuplicates = false, bool insertPrimaryKey = false)`
- `Insert(IDbConnection dbConnection, List<T> values, ..., bool ignoreDuplicates = false, bool insertPrimaryKey = false)`
- `Insert(IDbConnection dbConnection, IEnumerable<T> values, ..., bool ignoreDuplicates = false, bool insertPrimaryKey = false)`
- `Update(IDbConnection dbConnection, T value, string? dbTableName = null)`
- `Update(IDbConnection dbConnection, IEnumerable<T> values, string? dbTableName = null)`
- `Delete(IDbConnection dbConnection, T value, string? dbTableName = null)`
- `Delete(IDbConnection dbConnection, IEnumerable<T> values, string? dbTableName = null)`
- `Delete(IDbConnection dbConnection, ..., [filter params])`

### Insert Options

- `ignoreDuplicates`: emits `INSERT OR IGNORE`.
- `insertPrimaryKey`: includes PK in insert statement instead of identity behavior.

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
- `Populate(IDbConnection dbConnection, string? dbTableName = null, string? sourceTableName = null, bool sanitizeText = false)`
- `Select(IDbConnection dbConnection, List<string> matchTerms, string? dbTableName = null, string[]? orderByProperties = null, string? orderByDirection = null)`
- `SelectCount(IDbConnection dbConnection, List<string> matchTerms, string? dbTableName = null)`

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
