# Dependencies

This file catalogs direct package dependencies by project and purpose.

## Shared Build Properties (`src/common.props`)

- `TargetFramework`: `netstandard2.0` (base for generator library)
- `LangVersion`: `14.0`
- `Nullable`: enabled
- `ImplicitUsings`: enabled
- package metadata: repository URL, author/company, timestamp-based versioning

## Project: `src/cslightdbgen.sqlitegen`

### NuGet Packages

- `Microsoft.CodeAnalysis.Common` `4.14.0` (`PrivateAssets=all`)
- `Microsoft.CodeAnalysis.CSharp` `4.14.0` (`PrivateAssets=all`)

### Purpose

Provides Roslyn APIs for incremental syntax/semantic analysis and source emission.

## Project: `tests/cslightdbgen.sqlitegen.tests`

### NuGet Packages

- `Microsoft.NET.Test.Sdk` `17.14.1`
- `xunit` `2.7.1`
- `xunit.runner.visualstudio` `2.5.8`
- `FluentAssertions` `8.3.0`
- `Microsoft.CodeAnalysis.CSharp` `4.14.0`

### Purpose

Compiles test fixtures, runs generator in-memory, and validates generated source + diagnostics.

## Project: `tests/cslightdbgen.sqlitegen.integration`

### NuGet Packages

- `FluentAssertions` `8.3.0`
- `Microsoft.Data.Sqlite` `9.0.10`
- `Microsoft.NET.Test.Sdk` `17.14.1`
- `xunit` `2.7.1`
- `xunit.runner.visualstudio` `2.5.8`

### Purpose

Executes generated APIs against in-memory SQLite for runtime contract validation.

## Project: `tests/cslightdbgen.performance`

### Target Framework

- `net10.0`

### NuGet Packages

- `BenchmarkDotNet` `0.15.6`
- `Dapper` `2.1.66`
- `Dapper.SqlBuilder` `2.1.66`
- `Microsoft.Data.Sqlite` `10.0.0`
- `Microsoft.EntityFrameworkCore` `10.0.0`
- `Microsoft.EntityFrameworkCore.Sqlite` `10.0.0`

### Purpose

Compares generated code performance with Dapper and EF Core baselines across insert/select scenarios.

## Project Reference Dependencies

- Test and benchmark projects reference generator project as analyzer:
  - `OutputItemType="Analyzer"`
  - `ReferenceOutputAssembly="false"`

This is intentional so consumer test/benchmark projects compile with generated code rather than linking generator assembly runtime APIs.

## Operational Dependency Notes

- Use an SDK that can build all target frameworks in the solution (notably `net10.0` for benchmark project).
- SQLite runtime behavior in integration/performance tests depends on `Microsoft.Data.Sqlite` package versions listed above.
