# Changelog

All notable changes to `cslightdbgen.sqlitegen` are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project versions releases by build date (`yyyy.MMdd.HHmm`) rather than
SemVer, so entries are grouped by the version actually published to
[nuget.org](https://www.nuget.org/packages/cslightdbgen.sqlitegen). `[Unreleased]`
collects user-visible changes newer than the latest published version.

## [Unreleased]

### Added

- **Transaction enrollment.** Generated reads and writes accept an optional
  trailing `IDbTransaction?` and enroll in a caller-supplied transaction so
  multiple calls compose atomically; when omitted, each write keeps its previous
  auto-commit behavior.
- **`[LdgSQLiteDefault(value?, raw?)]`.** Emits a column `DEFAULT` clause
  (SQL-quoted literals, `bool` → `1`/`0`, numeric as-is, or `raw: true` for an
  unquoted SQL expression such as `CURRENT_TIMESTAMP`).
- **Foreign-key referential actions and composite foreign keys.**
  `LdgSQLiteForeignKey` gains `onDelete`/`onUpdate` (`LdgSQLiteFkAction`), and the
  new class-level `[LdgSQLiteForeignKeyComposite]` declares multi-column foreign
  keys.
- **Composite primary keys.** Two or more `[LdgSQLiteKey]` properties emit a
  table-level `PRIMARY KEY (...)` constraint; by-key `Update`/`Delete` match the
  full key.
- **Composite `UNIQUE` constraints and unique/partial indexes.** Class-level
  `[LdgSQLiteUnique(cols)]` emits a table `UNIQUE (...)` constraint;
  `[LdgSQLiteIndex]` gains `Unique` and `Where` for `CREATE UNIQUE INDEX` and
  partial indexes.
- **`EnsureSchema`.** Additive, non-destructive migration: `CREATE TABLE IF NOT
  EXISTS`, `ALTER TABLE … ADD COLUMN` for missing columns, and index
  (re)creation. Idempotent; never drops or retypes columns.
- **`Upsert`.** A single `INSERT … ON CONFLICT DO UPDATE/NOTHING` (requires
  SQLite ≥ 3.24) with `conflictColumns`, `updateColumns`, and accumulating
  `incrementColumns` control.
- **`InsertReturning` / `UpdateReturning`.** Write plus a trailing
  `RETURNING <all columns>` (requires SQLite ≥ 3.35) in one round-trip, returning
  a nullable `T?` hydrated in place (or `null` when no row is produced). Bulk
  `Insert` overloads now surface each row's generated identity key back onto the
  passed instances.
- **`NOT IN` list filters.** `{Name}NotInValues` parameters complement the
  existing `IN` (`{Name}Values`) filters for multi-select columns.
- **Multi-column `ORDER BY` with per-column direction.** Pass `orderByProperties`
  together with `orderByDirections` to emit `ORDER BY a DESC, b ASC`.
- **CSLDG diagnostics (CSLDG001/003/005/006).** The generator now reports a
  malformed or unsupported model as a diagnostic and skips it gracefully instead
  of aborting generation with a `CS8785`.
- **Documented multi-project analyzer topology.** Guidance for hosting the
  analyzer in a single project and silencing the benign `CS0436` when it runs in
  two mutually-referencing projects.

### Changed

- Generator Roslyn reference `Microsoft.CodeAnalysis` bumped `5.3.0` → `5.6.0`,
  which raises the minimum Roslyn/SDK required to run the generator.

### Fixed

- `DateTimeOffset` values preserve their original offset on read.
- Enum collections and arrays persist as JSON.
- Keyed by-key `Update`/`Delete` throw a typed `LdgCommandFailedException` when
  they affect zero rows (a stale/already-deleted key); opt out per call with
  `throwOnZeroRowsAffected: false`. Keyless models never throw.
- Single-column foreign-key reference values resolve semantically from the model.
- Valid SQL is emitted for degenerate model shapes (keyless, primary-key-only,
  no non-key columns).
- All supported scalar and primary-key types compile and round-trip correctly,
  and single-object `Delete` binds the primary key for every key shape.
- `RETURNING` writes return `null` (nullable `T?`) rather than a default instance
  when no row is produced.
- Provider errors from `LoadMaxKey` propagate instead of being swallowed.
- SQL identifier quoting hardened across table, column, and default-table-name
  sites.
- Generated methods dispose their `IDbCommand` deterministically.

### Removed

- Unreachable CSLDG002 and CSLDG004 diagnostic descriptors.

## [2026.622.1509] - 2026-06-22

### Changed

- Generator Roslyn reference `Microsoft.CodeAnalysis` bumped `4.14.0` → `5.3.0`,
  raising the minimum Roslyn/SDK required to run the generator.

> The test and benchmark projects also swapped their SQLite provider to
> `SourceGear.sqlite3` to drop the unmaintained, CVE-bearing
> `SQLitePCLRaw.lib.e_sqlite3`. This is internal to those projects and not
> consumer-facing — the generator itself has zero runtime dependencies.

## [2026.416.1848] - 2026-04-16

### Added

- `[LdgSQLiteMultiSelect]` attribute — generates `IN (...)` multi-key filter
  parameters (`{Name}Values`) on filter/delete helpers.

## [2026.324.2022] - 2026-03-24

### Added

- FTS5 tokenizer support: `[LdgSQLiteFtsTable(sourceTable, tableName?, tokenizer?)]`
  emits the FTS5 `tokenize=` option.

### Fixed

- NuGet analyzer packaging and build fixes.

## [2026.323.2043] - 2026-03-23

### Added

- `orderByProperties` values are validated against real column names (invalid
  entries are ignored), and generated models expose `SQLiteColumnNames`.

## [2026.306.1728] - 2026-03-06

### Added

- Initial published generator: full CRUD (`CreateTable`/`DropTable`,
  `Insert`/`Update`/`Delete`, `SelectSingle`/`SelectList`/`SelectEnumerable`/
  `SelectDict`/`SelectCount`), filtered reads (numeric operators, nullable
  tri-state, `LIKE`, paging, ordering), FTS5 full-text search, the core attribute
  set, a multi-target (`net8.0`/`net9.0`/`net10.0`) test suite, CI, and NuGet
  analyzer packaging.
