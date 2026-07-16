# Dependencies

This file catalogs direct package dependencies by project and purpose. Versions mirror the
`.csproj` files — update this document whenever a `PackageReference` version changes.

## Shared Build Properties (`src/common.props`)

- `TargetFramework`: `netstandard2.0` (base for generator library)
- `LangVersion`: `14.0`
- `Nullable`: enabled
- `ImplicitUsings`: enabled
- package metadata: repository URL, author/company, timestamp-based versioning

## Project: `src/cslightdbgen.sqlitegen`

Roslyn incremental source generator. Targets `netstandard2.0` (analyzer-loading constraint) and is
packed as a NuGet analyzer (`analyzers/dotnet/cs`, `IsRoslynComponent=true`, `IncludeBuildOutput=false`).

### NuGet Packages

- `Microsoft.CodeAnalysis.Common` `5.6.0` (`PrivateAssets=all`)
- `Microsoft.CodeAnalysis.CSharp` `5.6.0` (`PrivateAssets=all`)

### Purpose

Provides Roslyn APIs for incremental syntax/semantic analysis and source emission. `PrivateAssets=all`
keeps the Roslyn assemblies out of the consumer's dependency closure — the analyzer runs inside the
compiler, not at runtime.

## Project: `tests/cslightdbgen.sqlitegen.tests`

Unit tests (generator contract verification). Multi-targets `net10.0;net9.0;net8.0`.

### NuGet Packages

- `Microsoft.NET.Test.Sdk` `18.8.1`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `3.1.5` (`PrivateAssets=all`)
- `Shouldly` `4.3.0`
- `Microsoft.CodeAnalysis.CSharp` `5.6.0`

### Purpose

Compiles test fixtures, runs the generator in-memory, and validates generated source + diagnostics.
References the generator as a plain `ProjectReference` because it hosts the generator directly (needs
the Roslyn APIs).

## Project: `tests/cslightdbgen.sqlitegen.integration`

Integration tests (runtime SQLite behavior). Multi-targets `net10.0;net9.0;net8.0`.

### NuGet Packages

- `Microsoft.Data.Sqlite.Core` `10.0.10`
- `SQLitePCLRaw.provider.e_sqlite3` `2.1.11`
- `SourceGear.sqlite3` `3.53.3`
- `Shouldly` `4.3.0`
- `Microsoft.NET.Test.Sdk` `18.8.1`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `3.1.5` (`PrivateAssets=all`)

### Purpose

Executes generated APIs against in-memory SQLite for runtime contract validation. References the
generator as an analyzer (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`). See the
SQLite provider composition note below.

## Project: `tests/cslightdbgen.performance`

BenchmarkDotNet comparisons. Targets `net10.0` (`OutputType=Exe`).

### NuGet Packages

- `BenchmarkDotNet` `0.15.8`
- `Dapper` `2.1.79`
- `Dapper.SqlBuilder` `2.1.66`
- `Microsoft.Data.Sqlite.Core` `10.0.10`
- `Microsoft.EntityFrameworkCore` `10.0.10`
- `Microsoft.EntityFrameworkCore.Sqlite.Core` `10.0.10`
- `SQLitePCLRaw.provider.e_sqlite3` `2.1.11`
- `SourceGear.sqlite3` `3.53.3`

### Purpose

Compares generated code performance with Dapper and EF Core baselines across insert/select scenarios.
References the generator as an analyzer (same as the integration project).

## SQLite Provider Composition

Both the integration and performance projects assemble the SQLite stack explicitly rather than
referencing the all-in-one `Microsoft.Data.Sqlite` package:

- **`Microsoft.Data.Sqlite.Core`** — the managed ADO.NET provider only, with no bundled native library.
- **`SQLitePCLRaw.provider.e_sqlite3`** — the SQLitePCLRaw shim that binds `Microsoft.Data.Sqlite.Core`
  to a native `e_sqlite3` export surface.
- **`SourceGear.sqlite3`** — supplies the native `e_sqlite3` binaries.

This deliberately avoids `SQLitePCLRaw.lib.e_sqlite3` — the native bundle that `Microsoft.Data.Sqlite`
pulls in transitively — which is no longer actively maintained and carries a known CVE.
`SourceGear.sqlite3` is an actively-maintained native build exposing the same `e_sqlite3` entry points,
so no provider-registration code changes are required. The performance project uses
`Microsoft.EntityFrameworkCore.Sqlite.Core` (not `…Sqlite`) for the same reason: it omits the bundled
native library so the `SourceGear.sqlite3` build is used instead.

## Project Reference Dependencies

- The integration and performance projects reference the generator project as an analyzer:
  - `OutputItemType="Analyzer"`
  - `ReferenceOutputAssembly="false"`
  - so they compile against generated code rather than linking the generator's runtime APIs.
- The unit-test project references the generator as a plain `ProjectReference`, because it hosts and
  runs the generator in-memory against test fixtures.

## Operational Dependency Notes

- Use an SDK that can build every target framework in the solution (`net10.0`, plus `net9.0` and
  `net8.0` for the multi-targeted test projects).
- SQLite runtime behavior in the integration/performance tests depends on the provider composition
  documented above; keep `Microsoft.Data.Sqlite.Core`, the SQLitePCLRaw provider, and
  `SourceGear.sqlite3` versions mutually compatible.
