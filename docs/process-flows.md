# Process Flows

## 1) Generation Flow (Build-Time)

```mermaid
flowchart TD
    A[Compilation Starts] --> B[Post-init emits attribute definitions]
    B --> C[Syntax provider scans class/record declarations]
    C --> D{Has generator class attribute?}
    D -- No --> E[Skip]
    D -- Yes --> F[Semantic model extraction]
    F --> G[Collect properties, keys, indexes, FK, nullability, JSON flags]
    G --> H[Walk base type chain for inherited members]
    H --> I{Table model or FTS model}
    I -- Table --> J[Emit CRUD/select/filter generated source]
    I -- FTS --> K[Emit FTS create/populate/select generated source]
    J --> L[Generated .g.cs compiled]
    K --> L
```

### Trigger Conditions

A class/record is considered for generation when it has class-level attributes that match:

- `LdgSQLiteTable`
- `LdgSQLiteFtsTable`
- (base marker supported): `LdgSQLiteBaseClass`

## 2) Runtime CRUD Flow (Generated Table Models)

```mermaid
sequenceDiagram
    participant App as Consumer code
    participant Model as Generated Model API
    participant DB as IDbConnection/SQLite

    App->>Model: CreateTable(db)
    Model->>DB: CREATE TABLE + CREATE INDEX

    App->>Model: Insert/Update/Delete/Select*(...filters)
    Model->>DB: Parameterized SQL commands
    DB-->>Model: Rows/scalars
    Model-->>App: Model instances / counts / ids
```

### Key Behavior

- Insert overloads: single value, `List<T>`, `IEnumerable<T>`.
- Select overloads: single, list, enumerable, dictionary, count.
- Filter model generated per property:
  - equality and optional `LIKE` for strings,
  - numeric operator switch (`=`, `!=`, `>`, `>=`, `<`, `<=` and named aliases),
  - nullable tri-state (`PropertyIsNull == true/false/null`),
  - `PropertyValues` list for `IN (...)` on `*Key` style columns.

## 3) Runtime FTS Flow (Generated FTS Models)

```mermaid
sequenceDiagram
    participant App as Consumer code
    participant FTS as Generated FTS API
    participant DB as IDbConnection/SQLite

    App->>FTS: CreateTable(db)
    FTS->>DB: CREATE VIRTUAL TABLE ... using fts5

    App->>FTS: Populate(db, sourceTable, sanitizeText?)
    FTS->>DB: INSERT INTO fts SELECT FROM source
    Note over FTS,DB: sanitizeText=true strips HTML per text value

    App->>FTS: Select(db, matchTerms)
    FTS->>DB: WHERE table MATCH $matchTerm0 AND ...
    DB-->>FTS: Results
    FTS-->>App: List<T>
```

## 4) Test Flow

```mermaid
flowchart LR
    A[Unit tests] --> B[Compile fixture code in-memory]
    B --> C[Run generator driver]
    C --> D[Assert generated source strings + compile diagnostics]

    E[Integration tests] --> F[Compile with analyzer reference]
    F --> G[Run against in-memory SQLite]
    G --> H[Assert CRUD/FTS behavior end-to-end]
```

## 5) Benchmark Flow

```mermaid
flowchart LR
    A[BenchmarkSwitcher] --> B[SingleInsert]
    A --> C[BulkInsert]
    A --> D[SingleRecordSelect]
    A --> E[MultiRecordFilteredSelect]
    A --> F[MultiRecordUnfilteredSelect]

    B --> G[Compare SqliteGen vs Dapper vs Dapper.SqlBuilder vs EF Core]
    C --> G
    D --> G
    E --> G
    F --> G
```

Benchmark outputs are written under `BenchmarkDotNet.Artifacts/results`.
