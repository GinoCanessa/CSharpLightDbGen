# Onboarding Guide

This guide is intended for developers starting work on `CSharpLightDbGen`.

## 1) What This Project Does

`CSharpLightDbGen` generates SQLite data-access code at compile time for attributed `partial` classes/records. It provides:

- schema lifecycle helpers (`CreateTable`, `DropTable`),
- query helpers (`SelectSingle/List/Enumerable/Dict/Count`),
- mutation helpers (`Insert`, `Update`, `Delete`),
- optional full-text-search generation via SQLite FTS5.

## 2) First-Day Setup

1. Install a .NET SDK that supports all target frameworks in this repo.
2. Clone repository and open root folder in VS Code.
3. From repository root run:

```powershell
dotnet build .\db-gen.slnx -c Release
dotnet test .\tests\cslightdbgen.sqlitegen.tests\cslightdbgen.sqlitegen.tests.csproj -c Release
dotnet test .\tests\cslightdbgen.sqlitegen.integration\cslightdbgen.sqlitegen.integration.csproj -c Release
```

If these pass, your local dev environment is ready.

## 3) Learn the Codebase in 60 Minutes

### Step A — Generator internals (25 min)

Read in order:

1. `src/cslightdbgen.sqlitegen/GeneratorAttributes.cs`
2. `src/cslightdbgen.sqlitegen/LightSQLiteGenerator.cs`

Focus on:

- syntax target predicates for classes/records,
- semantic metadata extraction,
- generated SQL and method templates,
- table vs FTS generation branches.

### Step B — Unit contracts (15 min)

Read:

- `tests/cslightdbgen.sqlitegen.tests/TestFixtures/FixtureSources.cs`
- `tests/cslightdbgen.sqlitegen.tests/LightSQLiteGenerator_GenerationTests.cs`
- `tests/cslightdbgen.sqlitegen.tests/LightSQLiteGenerator_FilterParityTests.cs`
- `tests/cslightdbgen.sqlitegen.tests/LightSQLiteGenerator_FtsTests.cs`

These define expected generated source features and API signatures.

### Step C — Runtime behavior (10 min)

Read:

- `tests/cslightdbgen.sqlitegen.integration/IntegrationModels.cs`
- `tests/cslightdbgen.sqlitegen.integration/LightSQLiteGenerator_IntegrationTests.cs`

These show real usage against SQLite in-memory DB.

### Step D — Performance context (10 min)

Read:

- `tests/cslightdbgen.performance/Benchmarks.cs`
- benchmark reports under `BenchmarkDotNet.Artifacts/results`

This gives baseline comparisons versus Dapper and EF Core.

## 4) Common Development Tasks

### Add a new generator behavior

1. Modify generation logic in `LightSQLiteGenerator.cs`.
2. Add/update fixture in `FixtureSources.cs`.
3. Add/update unit assertions in generation/filter/fts tests.
4. Add integration coverage when behavior affects runtime SQL semantics.
5. Run unit + integration tests.

### Add a new model attribute

1. Add constant and generated attribute definition in `GeneratorAttributes.cs`.
2. Include attribute in detection sets if needed (`_ldAttributes`, `_ldClassAttributes`).
3. Thread new metadata into generator semantic analysis.
4. Emit SQL/API behavior.
5. Add tests for compile and runtime paths.

### Investigate runtime behavior differences

1. Reproduce with integration tests first.
2. Inspect generated source from unit test host output for affected fixture.
3. Compare SQL emitted by generated method branch.
4. Add targeted regression test.

## 5) Process and Quality Expectations

- Prefer additive, tested changes over broad refactors.
- Preserve generated API signatures unless intentionally versioning behavior.
- Keep SQL value inputs parameterized.
- Treat interpolated table/order fragments as internal-only trusted inputs.
- Add both unit (source-level) and integration (runtime-level) tests for behavior changes.

## 6) Gotchas You Should Know Early

- Generator matching uses attribute name strings; attribute naming/qualification style matters.
- Inheritance member collection is based on base-type name lookup.
- Complex/custom properties are serialized as JSON and parsed back.
- FTS `sanitizeText=true` path strips HTML tags before insert.
- Integration tests currently expect single-object delete overloads to throw under current runtime behavior; do not “fix” this without changing intended contract and tests.

## 7) Where to Go Next

After onboarding, continue with:

- [Architecture Overview](./architecture.md)
- [Process Flows](./process-flows.md)
- [Generated API Contract](./api-contracts.md)
- [Commands and Options](./commands.md)
- [Dependencies](./dependencies.md)
