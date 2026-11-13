# Release qualification and plan: 2026.1112.1700

**Historical record:** the publication instructions below describe the
November 12 clock-based workflow. They are superseded by the
[tagged release process](./releasing.md); do not follow them for a new release.
The local qualification results and artifact identity remain unchanged.

Qualified locally on **2026-11-12**, from branch `dev`, commit
`73c5d34c1c68de860818579eaa091f950c7b814c`. The worktree was clean before
qualification; this preparation changes documentation only. Nothing has been
committed, tagged, pushed, or published by this qualification.

**Status:** local code and package checks pass. Fresh cross-platform CI and
the final published artifact remain release gates. No GitHub Actions run was
found for this candidate SHA at qualification time. The latest published
NuGet version was `2026.716.1308`.

## Tag and package version

This plan treats **`2026.1112.1700` as the requested Git tag**, not an override
of the NuGet version. The local pack actually produced
**`cslightdbgen.sqlitegen.2026.1112.1759.nupkg`**.

`src/common.props` derives versions from the packing machine's local clock.
The existing publisher rebuilds on an Ubuntu runner; its package can have a
different timestamp or even a different date from November 12 in the
maintainer's time zone. A tag does not select or trigger a package version.
Record the final tag-to-package mapping explicitly.

If `2026.1112.1700` must also be the **NuGet version**, stop before publication:
the current documented process cannot promise it. That requires an explicit,
separately approved versioning/publication change, not a renamed archive,
backdated clock, or premature changelog release header.

## Qualification evidence

| Check | Result |
|---|---|
| Release solution build, SDK `11.0.100`, Windows | Pass; 0 errors, 166 existing `CS8602` warnings at clean source HEAD |
| Unit tests, `net8.0`, `net9.0`, `net10.0` | 314 passed per framework; 942 executions |
| SQLite integration tests, all three frameworks | 78 passed per framework; 234 executions |
| Actual NuGet consumption, SDK `10.0.404`, all three frameworks | The same 78 integration tests passed per framework; 234 additional executions |
| Analyzer load and cross-host compilation | No `CS8784`, `CS8785`, `CS9057`, or `CS0436` warnings in package-consumer output |
| Analyzer release tracking | No `RS20xx` warnings in build or pack output; CSLDG007/008 remain correctly unshipped |
| Archive layout | One analyzer DLL under `analyzers/dotnet/cs`, plus `README.md` and `LICENSE`; no `lib/`, `ref/`, or `runtimes/` payload |
| Package dependencies | Empty `.NETStandard2.0` dependency group; no shipped Roslyn assemblies or NuGet dependencies |
| Source provenance | `.nuspec` repository commit matches the qualified source SHA |
| NuGet advisory inventory | No vulnerable packages reported across the solution using nuget.org at qualification time |

No tests failed or were skipped. The 166 warnings were observed before any
documentation edits and are the current source baseline, not a regression
introduced by this release preparation.

The package-consumer fixture was isolated outside the repository. It linked
the unchanged integration sources and recreated the A/B host projects, using
the exact local package via `PackageReference` in all three projects instead
of an analyzer `ProjectReference`. It covered ordinary CRUD, JSON policies,
key constraints, FTS, transactions, independently hosted annotations, and the
existing assertions that consumer assemblies do not reference the generator
at runtime. SDK 10 was selected only in that isolated fixture; no repository
SDK pin was added. This does not claim compatibility with every older SDK.

Local artifact: `nupkg\cslightdbgen.sqlitegen.2026.1112.1759.nupkg`.

SHA-256:

```text
CB7737C922AAC38DEC10526ED87463A079E333F1466FDD0F3615C1DF629876CD
```

The artifact is ignored build output, not a published release. A new pack
requires recording its own version and checksum. The package description
still reads `Package Description`; this is existing metadata polish to
consider before publication, not a functional qualification failure.

## Release scope

The updated `[Unreleased]` changelog describes model-local JSON options,
primary keys without injected extra constraints, and opt-in independent
analyzer hosts. Existing defaults remain unchanged. CSLDG007 and CSLDG008
are new error diagnostics. No dependency-version change is part of this
candidate; the generator remains a dependency-free `netstandard2.0` analyzer.

## Step-by-step release

1. **Approve the identity.** Confirm that `2026.1112.1700` is the Git tag
   and that the NuGet version will be observed separately. If identical
   versions are required, resolve the versioning gate above first. Keep
   `[Unreleased]` and the analyzer ledgers unrolled until publication.
2. **Finalize the candidate.** Review and commit the changelog and release
   documentation. Include the already committed feature work in the release
   PR to `main`. Publishing that branch or opening its PR is a separate,
   user-initiated action through `dev-pr-open`; neither happened here.
3. **Run fresh CI.** Require the candidate's `Tests` matrix to pass on
   Ubuntu, Windows, and macOS, with .NET 8/9/10 tests. Older branch runs
   do not qualify this SHA. Documentation-only and tag-only pushes are
   outside the workflow's path filters. Requalify code or packaging changes.
4. **Control the publication handoff before merging.** A successful `main`
   `Tests` run automatically starts `Publish dotnet generator`. Arrange
   a quiet `main` branch through tests and publication, and require the
   publisher checkout to match the tested release commit: the current
   workflow does not pin checkout to `workflow_run.head_sha`. If that
   cannot be assured, fix the handoff before permitting this release.
5. **Publish exactly once.** Merge the approved release PR only when ready
   for that automatic publication. Let the existing workflow pack and push
   using its configured secret; do not also manually dispatch it. A tag
   does not publish, and the local `1759` package is not the archive this
   workflow will push. Verify the publisher result and capture its package
   version and source SHA.
6. **Verify the published artifact.** Confirm the actual version appears
   on nuget.org, download that exact package, and record its SHA-256.
   Verify its `.nuspec` source commit and dependency-free analyzer layout.
   Repeat the package-consumer integration checks against this exact
   downloaded version on .NET 8/9/10; the local checksum above is not
   evidence for a separately rebuilt CI archive. If verification fails,
   stop the release announcement and investigate rather than republishing
   over an existing NuGet version.
7. **Roll the release records.** Follow [the post-publish checklist](./releasing.md#roll-changelogmd):
   move the full `[Unreleased]` body under the actual NuGet version and
   its encoded date, then leave `[Unreleased]` empty. Move CSLDG007 and
   CSLDG008 verbatim into `AnalyzerReleases.Shipped.md` under that same
   version; leave only the two comment lines in `AnalyzerReleases.Unshipped.md`.
   Do not use the Git tag as the package-version header unless they match.
8. **Verify and commit bookkeeping.** Run the documented generator build
   and inspect its output for any `RS20xx` warning, not just a successful
   exit code. Commit the changelog and ledger roll separately. This
   follow-up changes only Markdown files, so it is outside the current
   `Tests` path filters and does not by itself start another automatic
   publish. Keep unrelated `.cs` or `.csproj` changes out of that commit
   and its publication handoff.
9. **Tag the published source and announce.** Create the annotated Git tag
   `2026.1112.1700` at the commit identified by the verified package's
   `.nuspec`, not a later unrelated or bookkeeping commit. In the annotation
   and GitHub release notes, record the actual NuGet version, source SHA,
   checksum, and the three release features. Only push the tag and create
   the GitHub release with explicit publishing authorization.

For future local reruns, use the Release build/test and local pack commands
in [Commands and Options](./commands.md). Publication behavior and the full
ledger-roll rules are maintained in [Release Checklist](./releasing.md).
