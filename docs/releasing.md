# Release Checklist

This is the **post-publish bookkeeping** checklist for `cslightdbgen.sqlitegen`.
It starts *after* a package has been pushed to nuget.org: the repository
documents no pack or push command, and `AGENTS.md` forbids inventing one, so
this page deliberately covers only the record-keeping that must follow a
publish. If pack and push commands are ever standardized, they belong in
[Commands and Options](./commands.md) first and should be referenced from here.

## Before Publishing

Confirm the solution is green before producing a package.

```powershell
dotnet build db-gen.slnx
```

```powershell
dotnet test db-gen.slnx
```

## The Version Is Not Chosen, It Is Observed

`VersionPrefix` is computed as `yyyy.MMdd.HHmm` in `src/common.props` at build
time, so the version is whatever the packing build's clock said. Never
hand-edit a version number.

Read the exact version string off the produced package and use that same
string, unchanged, in both bookkeeping steps below.

## Roll `CHANGELOG.md`

1. Insert `## [<version>] - <yyyy-MM-dd>` immediately beneath
   `## [Unreleased]`, using the observed version string and the date it
   encodes.
2. Move the entire accumulated `[Unreleased]` body under the new header. Do not
   reword entries.
3. Leave `[Unreleased]` empty — header line, blank line, then the version
   header. Do not seed empty `### Added` / `### Changed` / `### Fixed`
   subheadings; an empty subheading reads as "nothing landed under Added",
   which is a weaker signal than an empty section.

## Roll the Analyzer Ledgers

1. Move every rule from `src/cslightdbgen.sqlitegen/AnalyzerReleases.Unshipped.md`
   into `src/cslightdbgen.sqlitegen/AnalyzerReleases.Shipped.md` under a new
   `## Release <version>` section.
2. Preserve each rule's Category, Severity, and Notes **verbatim**. A row that
   disagrees with the descriptor in `GeneratorDiagnostics.cs` trips RS2001.
3. Reduce `AnalyzerReleases.Unshipped.md` to its two `;` comment lines. That
   header-only shape is the unambiguous "nothing pending" state.

## Verify

```powershell
dotnet build src\cslightdbgen.sqlitegen\cslightdbgen.sqlitegen.csproj
```

Then confirm no `RS20` diagnostic appears in that build's output.

**A green build alone proves nothing here.** The release-tracking rules
RS2000–RS2008 ship at severity `warning`, and this repository sets no
`TreatWarningsAsErrors`, so a malformed release header, a duplicate rule, or a
category/severity mismatch produces a warning and still reports
`Build succeeded`. Inspect the output, not just the exit code.

## Why This Is a Checklist and Not a CI Gate

The release-tracking analyzer is structurally blind to a skipped roll: a rule
left in `AnalyzerReleases.Unshipped.md` forever is a perfectly valid state to
it. That blindness is exactly how `2026.716.1308` shipped with its rules still
unshipped and its changelog entries still under `[Unreleased]`.

Enforcement — failing a build when a published version has no matching
`CHANGELOG.md` header — was considered and deliberately deferred in favor of
this written checklist. Revisit it if the drift recurs.
