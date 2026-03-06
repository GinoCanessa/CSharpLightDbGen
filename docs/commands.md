# Commands and Options

This is the contributor command reference for build, test, and benchmark workflows.

## Environment Prerequisites

- .NET SDK installed (must support all project target frameworks, including `net10.0` for benchmark project).
- Windows PowerShell examples below assume the repository root as current directory.

## Build Commands

### Build entire solution

```powershell
dotnet build .\db-gen.slnx -c Release
```

### Build generator project only

```powershell
dotnet build .\src\cslightdbgen.sqlitegen\cslightdbgen.sqlitegen.csproj -c Release
```

### Build benchmark project

```powershell
dotnet build .\tests\cslightdbgen.performance\cslightdbgen.performance.csproj -c Release
```

## Test Commands

### Unit tests (generator contracts)

```powershell
dotnet test .\tests\cslightdbgen.sqlitegen.tests\cslightdbgen.sqlitegen.tests.csproj -c Release
```

### Integration tests (runtime SQLite)

```powershell
dotnet test .\tests\cslightdbgen.sqlitegen.integration\cslightdbgen.sqlitegen.integration.csproj -c Release
```

### Run all test projects in solution

```powershell
dotnet test .\db-gen.slnx -c Release
```

### Useful `dotnet test` options

- `--filter <expression>`: run a subset of tests.
- `--logger "trx"`: emit TRX logs.
- `--collect:"XPlat Code Coverage"`: collect coverage data.

Example:

```powershell
dotnet test .\tests\cslightdbgen.sqlitegen.tests\cslightdbgen.sqlitegen.tests.csproj -c Release --filter "FullyQualifiedName~Fts"
```

## Benchmark Commands

### Run benchmark suite

```powershell
dotnet run -c Release --project .\tests\cslightdbgen.performance\cslightdbgen.performance.csproj
```

### Pass BenchmarkDotNet arguments

Arguments can be passed through after `--`:

```powershell
dotnet run -c Release --project .\tests\cslightdbgen.performance\cslightdbgen.performance.csproj -- --filter *Single* --job short
```

### Common BenchmarkDotNet argument patterns

- `--filter <pattern>`: run selected benchmark classes/methods.
- `--job <name>`: choose a job profile.
- `--runtimes <tfm...>`: runtime selection when applicable.

Results are generated in:

- `BenchmarkDotNet.Artifacts/results`

## Package Inspection Commands

### List direct + transitive packages

```powershell
dotnet list .\tests\cslightdbgen.performance\cslightdbgen.performance.csproj package --include-transitive
```

### Check vulnerable packages

```powershell
dotnet list .\db-gen.slnx package --vulnerable --include-transitive
```

## Typical Contributor Loops

### Quick change validation (generator logic change)

1. `dotnet build .\src\cslightdbgen.sqlitegen\cslightdbgen.sqlitegen.csproj -c Release`
2. `dotnet test .\tests\cslightdbgen.sqlitegen.tests\cslightdbgen.sqlitegen.tests.csproj -c Release`
3. `dotnet test .\tests\cslightdbgen.sqlitegen.integration\cslightdbgen.sqlitegen.integration.csproj -c Release`

### Performance regression check

1. `dotnet build .\tests\cslightdbgen.performance\cslightdbgen.performance.csproj -c Release`
2. `dotnet run -c Release --project .\tests\cslightdbgen.performance\cslightdbgen.performance.csproj`
3. Compare outputs in `BenchmarkDotNet.Artifacts/results`
