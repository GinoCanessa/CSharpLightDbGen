# Release Checklist

This covers local package qualification, the existing publication automation,
and **post-publish bookkeeping** for `cslightdbgen.sqlitegen`. The local pack
command is documented in [Commands and Options](./commands.md#build-a-local-analyzer-package).
The [2026.1112.1700 candidate plan](./release-2026.1112.1700.md) records the
November 12 qualification and remaining release gates.

## Before Publishing

Confirm the solution is green before producing a package.

```powershell
dotnet build db-gen.slnx --configuration Release
```

```powershell
dotnet test db-gen.slnx --configuration Release
```

Then pack and inspect the archive and run package-based consumer checks.
Passing tests that reference the generator project alone does not qualify its
NuGet packaging.

## Publication Is Not Tag-Driven

`.github/workflows/nuget-generator.yaml` publishes automatically after a
successful `Tests` workflow on `main`, or on manual `workflow_dispatch`.
A tag push does not invoke it. Merging code into `main` can therefore publish
before a tag or GitHub release is created; do not also dispatch a second
publish for the same release.

The publisher currently packs on an Ubuntu runner with its own clock, then
pushes the newly built archive to nuget.org. It does not publish an already
qualified local archive, accept a version input, or derive a version from a
tag. Its checkout is not explicitly pinned to the triggering test run's
`head_sha`. Confirm source identity and version before relying on this path
for an exact-tag release. The `Tests` workflow filters on `.cs` and `.csproj`
changes; a documentation-only or tag-only push is not a fresh CI qualification.

## The Version Is Not Chosen, It Is Observed

`VersionPrefix` is computed as `yyyy.MMdd.HHmm` in `src/common.props` at build
time, so the version is whatever the packing build's clock said. Never
hand-edit a version number.

Read the exact version string off the produced package and use that same
string, unchanged, in both bookkeeping steps below.

If a release must use a predetermined **NuGet version**, stop until an approved
versioning and publication path can produce and qualify that exact artifact.
Do not rename a `.nupkg`, change the machine clock, or claim that a different
timestamped package has the requested version.

A Git tag is a separate label: creating `2026.1112.1700` does not force that
NuGet version. If the requested tag differs from the published package version,
record their mapping explicitly in the tag annotation and GitHub release
notes. The changelog and analyzer ledgers still use the actual package version.

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
