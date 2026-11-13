# Architecture Overview

## Purpose

`CSharpLightDbGen` is a Roslyn incremental source generator that emits strongly-typed SQLite data access APIs for attributed models.

The generated code targets runtime `IDbConnection` usage and avoids runtime reflection-heavy ORM patterns for core CRUD operations.

## Solution Structure

```text
src/
  cslightdbgen.sqlitegen/             # Source generator (Roslyn analyzer component)
tests/
  cslightdbgen.sqlitegen.tests/       # Unit-level generator contract tests
  cslightdbgen.sqlitegen.integration/ # End-to-end runtime SQLite behavior tests
  cslightdbgen.sqlitegen.integration.host-a/ # Local-annotation analyzer fixture
  cslightdbgen.sqlitegen.integration.host-b/ # Local host, ordinarily references A
  cslightdbgen.performance/           # BenchmarkDotNet performance comparisons
```

## Core Components

### 1) Generator Attributes (`GeneratorAttributes.cs`)

Provides attribute source text injected into consuming compilations:

- `LdgSQLiteBaseClass`
- `LdgSQLiteTable`
- `LdgSQLiteIndex`
- `LdgSQLiteKey`
- `LdgSQLiteKeyNoUnique`
- `LdgSQLiteJsonOptions`
- `LdgSQLiteRuntimeNamespace`
- `LdgSQLiteForeignKey`
- `LdgSQLiteForeignKeyComposite`
- `LdgSQLiteIgnore`
- `LdgSQLiteUnique`
- `LdgSQLiteMultiSelect`
- `LdgSQLiteDefault`
- `LdgSQLiteFtsTable`
- `LdgSQLiteFtsUnindexed`

Also supplies `LdgSQLiteFkAction`. These top-level types are **public by default**.
Project-wide `CSLIGHTDBGEN_MULTI_HOST` makes them internal, while their constructors/
properties remain public members. Public model DALs/extensions are unchanged; exception
ownership is separate (see [Multi-Project Placement](#4-multi-project-placement-cs0436)).

### 2) Incremental Generator (`LightSQLiteGenerator.cs`)

Implements `IIncrementalGenerator` and does the following:

1. Emits attribute definitions in post-initialization.
2. Uses `ForAttributeWithMetadataName` for regular tables and a narrow
   `CreateSyntaxProvider` for the FTS-annotated partial declaration, followed by semantic
   validation. FTS discovery is not a second metadata-name provider.
3. Builds value-equatable table/FTS metadata (columns, keys, nullability, JSON binding,
   inheritance and diagnostic descriptions).
4. Combines model descriptions with validated helper names and, for FTS, the
   sorted/deduplicated union of local and referenced table names. Table generation
   takes precedence over FTS for dual-annotated models.
5. Emits ordinary runtime helpers independently of model enumeration, and emits
   model-specific generated partials and extension wrappers.

Four bounded components separate policy from SQL templates:

| Component | Boundary |
|---|---|
| `GeneratorConfiguration` | `ResolveJsonOptionsBinding` inspects model/factory symbols, signatures and accessibility; `ResolveEnvironment` validates assembly namespace configuration/collisions. Only value-equatable descriptions leave semantic projection. |
| `TableSchemaPlanner` | `Plan` classifies ordered logical keys, composite/identity behavior and per-column constraint flags; returns no SQL fragments. All downstream key consumers share this classification. |
| `JsonPersistenceEmitter` | Emits unchanged default wrappers or one model-local lazy options snapshot from a validated binding; does not interpret attributes or own SQL/read shapes. |
| `RuntimeHelpersEmitter` | Emits the public exception in the resolved namespace; does not choose namespace policy or change failure guards. |

`GeneratorModel.cs` holds `GenerationEnvironment`, `JsonOptionsBinding`, diagnostic/
location descriptions and result wrappers that preserve diagnostics when configuration
is rejected. Emission-edge state contains strings, enums, booleans, `LocationInfo` and
`EquatableArray<T>`-based values, never Roslyn symbols, compilations, raw locations,
delegates or mutable collections. `ParseOptionsProvider` projects the project-wide
symbol; the compilation/environment projection may rerun after an edit, but value
equality stops unchanged output propagation. `LdgTableModel`/`LdgFtsModel` track
extraction separately from `LdgEnvironment`, `LdgRuntimeEmission`, `LdgTableEmission`
and `LdgFtsEmission`.

Invalid JSON binding (`CSLDG007`) suppresses only that model; invalid host configuration
(`CSLDG008`) suppresses ordinary helpers and dependent table/FTS outputs. Fixed post-init
annotations remain available for diagnostic binding in either case. SQL templates and
quoting remain in `LightSQLiteGenerator`.

### 3) Generated Runtime Surface

For regular table models (`[LdgSQLiteTable]`):

- DDL: create/drop table, index creation, additive `EnsureSchema` migration
- Queries: `SelectSingle`, `SelectList`, `SelectEnumerable`, `SelectDict`, `SelectCount`
- Mutations: `Insert`, `InsertReturning`, `Update`, `UpdateReturning`, `Upsert`, `Delete`
- Helpers: max-key loading, numeric operator mapping, JSON serialization/parsing helpers
- Extensions: convenience overloads on `IDbConnection`, model instances, and collections

For FTS models (`[LdgSQLiteFtsTable]`):

- DDL: `CREATE VIRTUAL TABLE ... using fts5`
- Population from source table (`Populate`)
- Search by term list (`MATCH`) and count
- Optional text sanitization with HTML stripping

### 4) Multi-Project Placement (CS0436)

Every analyzer host receives fixed conditional annotations in
`LdgSQLiteGeneratorAttributes.g.cs` through `RegisterPostInitializationOutput`.
`LdgSQLiteRuntimeHelpers.g.cs` is separate **ordinary output**, including in no-model hosts.

- **Default:** host the analyzer once and reference that project ordinarily to consume
  public annotations/DALs. Public exception identity remains
  `CsLightDbGen.SQLiteGenerator.LdgCommandFailedException`. Two default hosts with a
  one-way reference intentionally retain duplicate-public-type warning behavior.
- **Local opt-in:** both A and B append project-wide `CSLIGHTDBGEN_MULTI_HOST` and
  provide distinct `[assembly: LdgSQLiteRuntimeNamespace("...")]` annotations. B
  references A ordinarily; C references both. Annotations/FK enums are internal, but
  semantic discovery still reads referenced annotated base/key/table metadata. Each
  host owns its chosen **public** exception; no default-name alias is emitted. Public
  DALs/extensions compose without CS0436/CS0433, suppression, `extern alias`, friend
  access, consolidation or a runtime generator dependency.

Real A/B fixtures use `CsLightDbGen.Integration.HostA.Runtime` and
`CsLightDbGen.Integration.HostB.Runtime`; the integration assembly is a third,
default-mode analyzer consumer. Pure non-analyzer C is also covered by emitted-image
unit tests. Mixed default/local annotation hosts and friend exposure are not guaranteed
warning-free. Namespace adoption/changes require public catch/type-reference review.
See [setup](../README.md#multi-project-setup) and
[namespace validation](api-contracts.md#configuration-diagnostics).

### 5) JSON and Key Policies at Runtime

Direct model JSON binding emits a once-per-model execution-and-publication lazy copy
of consumer options. Only real serialization/deserialization accesses it; schema,
counters and sentinel guards do not. Initialization failures are cached without
fallback, not model-initializer poisoning. Snapshotting is shallow (shared converters);
original options remain independently mutable. Derived/FTS models select policies
explicitly. Existing null/empty wrappers and List-based collection reads remain intact.

Logical key membership is distinct from DDL decoration: `LdgSQLiteKeyNoUnique` omits
injected UNIQUE/NOT NULL even on CLR-nonnullable columns, while explicit uniqueness
and inherent PK/INTEGER rowid behavior persist. Old annotations, key CRUD, identity
hydration and counters are unchanged; composites use all old/new members without
identity. Nullable SQL text/composite keys do not imply NULL-safe CLR APIs.
`EnsureSchema` remains additive, not a key-constraint rebuild/repair mechanism.

## Runtime Boundary

The generator project only depends on Roslyn packages. Runtime DB behavior is in emitted code and uses ADO.NET abstractions (`IDbConnection`, `IDbCommand`, `IDataReader`).

This keeps the generator package lean and lets consuming applications choose their provider (`Microsoft.Data.Sqlite` in tests/benchmarks).

## Design Characteristics

- Compile-time code generation (no runtime schema discovery).
- Per-model static APIs for predictable invocation.
- Parameterized SQL for values.
- Optional string `LIKE` matching and numeric operator controls for filters.
- Optional `IN` list filters for `*Key` properties.
- Support for classes and `record class` models.

## Known Architectural Tradeoffs

- Regular table discovery uses `ForAttributeWithMetadataName`; FTS retains its narrow
  `CreateSyntaxProvider` attribute-spelling predicate before semantic projection.
  Base-class and syntax detection also retain name-string sets (`_ldAttributes` /
  `_ldClassAttributes`), so naming/qualification conventions still matter on residual paths.
- `orderByProperties` and table names are interpolated SQL fragments; they should be treated as trusted internal inputs.
- Base-member discovery is type-name based in generator logic, which can be ambiguous in duplicate-name namespace scenarios.
- **Auto-increment identity keys use a process-wide `static` counter (`_indexValue`) per generated
  model.** `GetIndex()` (`Interlocked.Increment`) hands out the next integer key, and
  `LoadMaxKey` — invoked by `EnsureSchema`/`CreateTable` for identity models — seeds it from
  `SELECT MAX(key)` on a single connection. Because the counter is shared across every
  `IDbConnection` used with that model in the process, the generated identity assignment assumes
  **one logical database per model type per process**. Using the same model against multiple
  databases concurrently (or mutating the table out-of-band) can desynchronize the counter; re-seed
  it with `LoadMaxKey` against the target connection, or supply keys explicitly. Natural-key
  (`string`/`Guid`), composite-key, and keyless models are inserted explicitly, never auto-assigned,
  and so are unaffected.
