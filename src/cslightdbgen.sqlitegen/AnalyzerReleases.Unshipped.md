; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CSLDG001 | CsLightDbGen.SQLiteGenerator | Error | Value-type column has no SQLite scalar mapping.
CSLDG002 | CsLightDbGen.SQLiteGenerator | Error | Unsupported or degenerate model shape.
CSLDG003 | CsLightDbGen.SQLiteGenerator | Error | Composite primary key combined with AutoIncrement.
CSLDG004 | CsLightDbGen.SQLiteGenerator | Error | Non-additive migration required for a populated table.
CSLDG005 | CsLightDbGen.SQLiteGenerator | Error | Unsupported key or foreign-key attribute combination.
CSLDG006 | CsLightDbGen.SQLiteGenerator | Error | FTS source table unresolved.
