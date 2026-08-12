# AGENTS.md

Canonical, machine-readable conventions for automated agents working in
**CSharpLightDbGen**. This file is the single source of truth that the
`.github/skills/dev-*` skills read before naming any build, test, or lint
command.

**Precedence.** This file is authoritative for commands, conventions, and
invariants an agent must follow. [`README.md`](README.md) and [`docs/`](docs/)
are authoritative for rationale, generated-API reference, and operational
detail, and are the place to look for the "why". If this file contradicts the
repository itself, the repository wins — fix this file.

---

## What this repository is

CSharpLightDbGen is a **Roslyn incremental source generator** that emits
strongly-typed SQLite data-access code at compile time for attributed C#
models — no runtime reflection and no runtime dependency on the generator
itself.

It ships as the **`cslightdbgen.sqlitegen` NuGet package**, consumed as an
analyzer. Two facts settle most arguments:

- **The generated code is the product.** A change is only real if it changes
  what the generator emits, or the diagnostics it reports. Assertions about
  behavior belong in tests that compile and run generated output.
- **It is a published package**, so the attribute surface
  (`GeneratorAttributes.cs`), the shape of generated members, and the
  `CSLDG###` diagnostic IDs are all **public API** — changing them is a
  breaking change for consumers.

---

## Repository layout

| Path | Contents |
|-|-|
| `db-gen.slnx` | The solution. All build/test commands target it. |
| `src/common.props` | Solution-wide MSBuild properties, imported by the generator project. |
| `src/cslightdbgen.sqlitegen/` | The generator/analyzer itself (`netstandard2.0`). |
| `src/cslightdbgen.sqlitegen/GeneratorAttributes.cs` | The `LdgSQLite*` attribute source injected into consumers. Public API. |
| `src/cslightdbgen.sqlitegen/GeneratorDiagnostics.cs` | `CSLDG###` diagnostic descriptors. Public API. |
| `src/cslightdbgen.sqlitegen/GeneratorModel.cs` | Equatable model types used for incremental caching. |
| `src/cslightdbgen.sqlitegen/LightSQLiteGenerator.cs` | The generator pipeline and emit logic (the bulk of the project). |
| `src/cslightdbgen.sqlitegen/AnalyzerReleases.{Shipped,Unshipped}.md` | Analyzer release-tracking ledgers. Build-enforced — see guardrails. |
| `tests/cslightdbgen.sqlitegen.tests/` | Unit tests: generator driver, emitted-source assertions, diagnostics. |
| `tests/cslightdbgen.sqlitegen.integration/` | Integration tests: generated code compiled and run against real SQLite. |
| `tests/cslightdbgen.performance/` | BenchmarkDotNet console app comparing against Dapper and EF Core. |
| `docs/` | Architecture, commands, API contracts, process flows, onboarding. |
| `CHANGELOG.md` | Keep a Changelog; `[Unreleased]` collects unpublished changes. |

`nupkg/` (the generator's `PackageOutputPath`), `bin/`, `obj/`, and
`/scratch` are gitignored. Never treat a path under `obj/` — including
generated `*.g.cs` under `obj/**/CsLightDbGen.SQLiteGenerator/` — as a source
file to edit; it is build output.

---

## Toolchain pins

- **No `global.json`.** There is no SDK pin. CI
  (`.github/workflows/build-and-test.yaml`) installs the **10.0, 9.0, and
  8.0** SDKs, so a local SDK must be able to build and test all three.
- **Generator target framework: `netstandard2.0`**, set in `src/common.props`.
  This is a Roslyn requirement, not a preference — see invariants.
- **Language version: C# `14.0`**, with `Nullable` and `ImplicitUsings`
  enabled, all set in `src/common.props`.
- **Test target frameworks: `net10.0;net9.0;net8.0`** (both test projects).
  `tests/cslightdbgen.performance` is `net10.0` only and is an `Exe`.
- **Roslyn: `Microsoft.CodeAnalysis.Common` / `.CSharp` 5.6.0**, referenced
  with `PrivateAssets="all"`. The same 5.6.0 version is referenced by the unit
  test project so the driver matches the generator.
- **Versioning is date-based**, not SemVer: `VersionPrefix` is computed as
  `yyyy.MMdd.HHmm` at build time in `src/common.props`. Do not hand-edit a
  version number.
- Dependency versions are declared per-`csproj`; there is no central package
  management and no lock file. `docs/dependencies.md` mirrors the `csproj`
  versions and must be regenerated when they change.

**Warnings are not errors.** There is no `TreatWarningsAsErrors` anywhere. The
generator project sets `NoWarn=$(NoWarn);NU5128` only.

---

## Build

```powershell
dotnet build db-gen.slnx
```

Scoped to a single project:

```powershell
dotnet build src\cslightdbgen.sqlitegen\cslightdbgen.sqlitegen.csproj
```

The expected baseline is **0 errors and roughly 91 warnings**. The warnings
are *pre-existing* and come almost entirely from generated `*.g.cs` in the
integration test project (`CS8602`, dereference of a possibly null reference),
multiplied across the three target frameworks. Do not treat them as a
regression you introduced without first confirming the count against a clean
`HEAD` — but **a change that raises the count is a finding**, because it means
the generator started emitting less-safe code.

CI additionally builds in Release across ubuntu/windows/macOS:

```powershell
dotnet build --configuration Release --no-restore
```

---

## Test

Both test projects use **xUnit 2.9.3 on VSTest** (`Microsoft.NET.Test.Sdk`
18.8.1 + `xunit.runner.visualstudio` 3.1.5), with `Shouldly` for assertions.
VSTest means **`--filter FullyQualifiedName~<substring>` is the valid filter
syntax** — this is *not* a Microsoft.Testing.Platform repo, so do not use
`-class` / `-method` executable flags.

Because the test projects multi-target, **always pass `-f net10.0`** for
scoped and focused runs. Without it every command runs three times.

### Full suite

```powershell
dotnet test db-gen.slnx
```

### Scoped — one project, one framework

```powershell
dotnet test tests\cslightdbgen.sqlitegen.tests\cslightdbgen.sqlitegen.tests.csproj -f net10.0
```

### Focused — one class or one test

```powershell
dotnet test tests\cslightdbgen.sqlitegen.tests\cslightdbgen.sqlitegen.tests.csproj -f net10.0 --filter "FullyQualifiedName~EnumRoundTrip"
```

**Prefer the smallest command that covers the change.** Escalate to the full
suite only when the focused run indicates you need to.

Which project to reach for:

- Changing **emitted source text or diagnostics** → `cslightdbgen.sqlitegen.tests`.
  It drives the generator in-process and asserts on the emitted strings and
  reported `CSLDG###` diagnostics.
- Changing **runtime behavior of generated code** (SQL correctness, type
  round-tripping, transactions, FTS) → `cslightdbgen.sqlitegen.integration`.
  It compiles generated code and executes it against real SQLite via
  `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.provider.e_sqlite3` +
  `SourceGear.sqlite3`. A generator change that only passes the unit tests is
  not verified.

No fixtures, containers, environment variables, or hermetic caches are
required — `dotnet test` is self-contained.

### Benchmarks

Benchmarks are **not** part of `dotnet test` and are not a gate. Run them
deliberately, in Release:

```powershell
dotnet run --project tests\cslightdbgen.performance -c Release
```

---

## Lint / format

No separate lint or format step, and no `.editorconfig` or formatter config.
The build and code review are the only enforcement. Do not add a linter
without being asked.

---

## Run

There is no application to run: the product is an analyzer that executes
inside the compiler. To exercise it, build or test a project that references
it, or inspect the emitted files under
`tests/**/obj/<config>/<tfm>/cslightdbgen.sqlitegen/CsLightDbGen.SQLiteGenerator/`.

Consumers wire it in as an analyzer, not a library reference:

```xml
<ProjectReference Include="path/to/cslightdbgen.sqlitegen.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

---

## Code style

There is no `.editorconfig`; the authoritative style is **the surrounding
file**. The conventions actually in force:

- **C#**: 4 spaces, no tabs. **MSBuild files (`.csproj`, `.props`)**: tabs.
  UTF-8, final newline.
- **Explicit types, never `var`.** The generator source contains zero `var`
  declarations; keep it that way.
- **`[]` for empty/collection initializers**, not `new List<T>()` or `new()`.
- Emitted code is built with raw/verbatim string blocks and
  `StringBuilder`/`IndentedTextWriter`-style appends in
  `LightSQLiteGenerator.cs`. When adding emit logic, match the neighbouring
  block's quoting and indentation handling exactly — generated-source
  whitespace is asserted by tests.
- Nullable reference types are enabled; do not silence a nullable warning in
  generator source with `!` where a real check will do.
- Match the surrounding file. Consistency with neighbouring code beats any
  general preference.

### Architectural invariants

These are decisions, not preferences. Violating one is a review Blocker.

- **The generator project stays `netstandard2.0`.** Roslyn loads analyzers
  into the compiler, which requires it. Any BCL API used in generator source
  must exist on `netstandard2.0` — no `net8.0+`-only APIs, no matter what
  `LangVersion 14.0` permits syntactically.
- **The generator ships no dependencies.** `Microsoft.CodeAnalysis.*` is
  referenced `PrivateAssets="all"`, `IncludeBuildOutput` is `false`, and the
  assembly is packed to `analyzers/dotnet/cs`. Adding a package reference that
  flows to consumers breaks analyzer loading. `EnforceExtendedAnalyzerRules`
  is on and will complain — do not suppress it.
- **Generated code takes no runtime dependency on this package.** It emits
  plain ADO.NET against `IDbConnection`/`IDbCommand`. Consumers supply their
  own SQLite provider.
- **The generator must stay incremental.** Values flowing through the pipeline
  must be value-equatable — this is what `EquatableArray<T>` and the model
  types in `GeneratorModel.cs` exist for. Never put a symbol, compilation, or
  reference-equality type into the pipeline state.
- **Generated code is synchronous and disposes deterministically.** There is
  no `CancellationToken` on generated methods, and every emitted `IDbCommand`
  is disposed. Keep both properties.
- **Identifiers are always quoted at emit sites** and user-supplied SQL
  fragments are parameterized. An emit path that concatenates an unquoted
  identifier or an unparameterized value is a security finding.
- **Diagnostic IDs are permanent.** A `CSLDG###` ID is never reused for a
  different meaning and never renumbered once shipped.

---

## Commit conventions

- **Conventional commits**: `<type>(<scope>): <subject>`. Types in active use
  are `feat`, `fix`, `refactor`, `test`, `docs`, and `chore`. Scope is
  optional but strongly encouraged, and is normally the affected component —
  `sqlitegen`, `perf`, `readme`, `changelog`. Subject in the imperative,
  target ≤ 72 characters.
- No repository-required commit trailers. (Tooling-added trailers such as
  `Co-authored-by:` are fine and are not governed here.)
- One logical change per commit.
- When the GitHub integration below is on **and** the slot carries an
  `Issue` binding, `dev-do` adds an `Issue: #N` trailer to each phase
  commit, in addition to every trailer required above. When the
  integration is off or the slot is unbound, nothing is added.
- Agents **do not push** and **do not open pull requests** unless the user
  explicitly asks. `dev-pr-open` is the one sanctioned exception, and only
  when the user invokes it.

---

## GitHub Integration

**Off by default, in two independent ways.** A repository whose
`AGENTS.md` has **no** `## GitHub Integration` section is off. A section
whose `Enabled` row says **`no`** is equally off. In either case no skill
prompts about GitHub, and the `dev-*` loop behaves exactly as it did
before this feature existed.

The block below is **machine-managed**. This section is the **normative
definition** of both sentinel strings: every skill that reads or writes
the block reproduces the opener and the closer byte-for-byte from here,
and no skill re-derives, paraphrases, or reformats them.

<!-- >>> dev-* github integration (managed by dev-* skills) >>> -->
| Setting | Value |
|-|-|
| Enabled | yes |
| Repository | GinoCanessa/CSharpLightDbGen |
| Label — feature request | `enhancement` |
| Label — bug report | `bug` |
| Label — docs-only (additive) | `documentation` |
| Changelog file | `CHANGELOG.md` |
| Changelog entry format | Keep a Changelog: a bullet under the `### Added` / `### Changed` / `### Fixed` subheading of `## [Unreleased]`, leading with a bolded noun phrase for the feature or API, then a sentence describing the user-visible effect. |
| PR opens as draft | no |
<!-- <<< dev-* github integration (managed by dev-* skills) <<< -->

**These sentinels are not `dev-setup`'s ignore-file sentinels.** The
ignore-file block that `dev-setup` maintains in `.gitignore` or
`.git/info/exclude` is delimited by
`# >>> dev-* skills (managed by dev-setup) >>>` and
`# <<< dev-* skills (managed by dev-setup) <<<`. That is a **different
block in a different file**, with a `#` comment prefix rather than an
HTML comment. Do not conflate the two, and never substitute one pair for
the other.

Rules for the block:

- Only `dev-setup`, `dev-issue`, and `dev-pr-open` may rewrite it, and
  only **in place** — never a second copy, never appended to the end of
  the file.
- Hand-written text outside the sentinels is never touched. Everything a
  human writes in this section survives every rewrite.
- A recorded value of `no`, `none`, or `n/a` is a **resolved answer**, not
  a missing one. It must never re-trigger a prompt on a later run.
- When `Enabled` is `no`, every other row is `n/a`.

Note on the changelog: `CHANGELOG.md` groups entries by the date-based version
actually published to nuget.org, and `[Unreleased]` collects everything newer
than the latest published version. Entries always go under `[Unreleased]`, and
a version header is never invented for unpublished work. The one time a version
header is created is the publish-time roll of `[Unreleased]` into the version
that was actually pushed — a defined step in `docs/releasing.md`.

---

## Scratch / slot convention

Local inner-loop work is organized into **slots** under `scratch/`:

```
scratch/<MMDD>-<##>/
  featurerequest.md    # authored by the dev-request skill
  bugreport.md         # authored by the dev-report skill
  plan.md              # authored by dev-plan, updated by dev-do
  analysis.md          # authored by dev-review
```

- `<MMDD>` is the local date (zero-padded month + day); `<##>` is a
  zero-padded two-digit slot number.
- `scratch/` is **ignored** (`/scratch` in `.gitignore`). Nothing in it is
  ever committed.
- Because the slot is ignored, **no plan phase may declare a `scratch/` path
  as an owned path.** `plan.md` is a control file that `dev-do` edits
  continuously and never stages or commits.

---

## Agent guardrails

- Read this file before proposing any build, test, or lint command. **Never
  invent a command.** If something you need is not documented here, say so
  rather than guessing.
- Subagents must use the same model configuration as the spawning agent.
- Do not add new linting, building, or testing tooling without being asked.
- Prefer the smallest targeted verification that covers the change; escalate
  to the full suite only when the targeted run indicates it is needed.
- **Adding or changing a `CSLDG###` diagnostic requires updating
  `AnalyzerReleases.Unshipped.md`** in the same change. Both ledgers are
  `AdditionalFiles` and the release-tracking analyzer fails the build when a
  descriptor is missing from them. On publish, rules move from `Unshipped` to
  `AnalyzerReleases.Shipped.md` under the released version header — that move,
  and the rest of the post-publish bookkeeping, is written down in
  `docs/releasing.md`.
- **`docs/` is expected to stay true.** `docs/dependencies.md` mirrors the
  `csproj` package versions; `docs/api-contracts.md`, `docs/commands.md`, and
  `docs/process-flows.md` describe generated API and generator behavior;
  `docs/releasing.md` describes the post-publish bookkeeping procedure.
  A change to any of those must update the corresponding doc.
- **`obj/` and `bin/` are off-limits for editing.** Generated `*.g.cs` under
  `obj/` is build output; to change it, change the generator.
- Do not hand-edit version numbers — they are computed from the build date.
