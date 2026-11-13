# Release Process

`cslightdbgen.sqlitegen` is published only by the **Publish SQLite generator**
workflow after a GitHub Release for **`v<version>`** is published. Merging to
`main`, pushing a tag, or creating a draft release does not publish to NuGet.
This follows the tagged-release mechanism in `dotnet-fhir-packages`, adapted
to a single analyzer package without its fixture or paired-package recovery
machinery.

Manual `workflow_dispatch` requires an existing tag and matching version and
is **validation-only**: it runs source tests, packs, and qualifies the package,
but cannot enter the publishing job. Maintainers dispatch and publish;
agents must not dispatch the release workflow or push packages locally.

## Administrator Preflight

Before the first release using this workflow:

1. Configure the protected GitHub environment **`nuget.org`** with required
   reviewers or equivalent deployment protection.
2. Set **`NUGET_PUBLISH_ENVIRONMENT_READY=true`** in that environment after
   configuring protection.
3. Store **`NUGET_USER`** as an environment secret. Its value is the nuget.org
   profile username, not an email address.
4. Configure a [NuGet trusted-publishing policy](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
   with repository owner **`GinoCanessa`**, repository **`CSharpLightDbGen`**,
   workflow filename **`nuget-generator.yaml`** (without the directory), and
   environment **`nuget.org`**. The policy's selected user or organization
   must own and permit publishing new versions of **`cslightdbgen.sqlitegen`**.
5. Allow `v*` release tags in the environment's deployment rules.

Only the publishing job has **`id-token: write`**. After revalidating the
qualified package, it uses **`NuGet/login@v1`** to exchange a GitHub OIDC token
for a temporary NuGet API key immediately before the push. New releases need
no long-lived API-key secret. The job fails closed without the readiness
variable, matching policy, username, or successful token exchange.
Neither source tests nor package qualification has OIDC publishing permission
or uses the publishing secret. No repository or NuGet settings are configured
by committing the workflow.

## Version and Source Identity

Keep the date-based three-component version convention, with canonical
numeric components and **no leading zeros**. For example, `yyyy.MMdd.HHmm`
at 09:00 on November 13, 2026 normalizes to `2026.1113.900`; its tag is
exactly `v2026.1113.900`. Components must be in `0-65534` for .NET assembly
metadata. Prerelease suffixes and unprefixed tags are not accepted.

Ordinary local builds still derive their version from the clock in
`src/common.props`. Release builds instead pass the approved version through
MSBuild's `VersionPrefix`, `Version`, and `PackageVersion`, and pass
`0.<version>` as both `AssemblyVersion` and `FileVersion`. The tag, NuGet
version, and analyzer version therefore agree regardless of the runner's
clock. Do not edit version numbers in the project, rename archives, or
backdate the machine.

The workflow freshly fetches the tag and `origin/main`, requires the tag
commit to be contained in `main`, and checks that the version is not already
published and is newer than existing canonical versions on NuGet. Release
events must match their original event SHA. Every source-test job checks out
that exact commit; the publisher rechecks the tag, ancestry, checkout, and
published GitHub Release before pushing.

## Prepare Release Records Before Tagging

Choose the release version, then prepare both records in the release commit.
These edits describe the candidate; they are not proof that publication has
succeeded. Merge the records to `main` before creating its tag.

### Roll `CHANGELOG.md`

1. Insert `## [<version>] - <yyyy-MM-dd>` immediately beneath
   `## [Unreleased]`, using the release version and date.
2. Move the entire accumulated `[Unreleased]` body under the new header,
   preserving entries.
3. Leave `[Unreleased]` empty: header, blank line, then the version header.
   Do not seed empty `### Added` / `### Changed` / `### Fixed` subheadings.

The validator requires exactly one matching release header at the tag commit.
Never reuse or relabel a historical published-version section.

### Roll the Analyzer Ledgers

1. Move all pending rules from
   `src/cslightdbgen.sqlitegen/AnalyzerReleases.Unshipped.md` into
   `src/cslightdbgen.sqlitegen/AnalyzerReleases.Shipped.md` under a new
   `## Release <version>` section, if there are pending rules.
2. Preserve each rule's Category, Severity, and Notes verbatim.
3. Leave only the two `;` comment lines in `AnalyzerReleases.Unshipped.md`.

The release validator rejects non-comment content in the unshipped ledger.
Releases without new diagnostics do not need an empty shipped section.

Verify the ledger edits:

```powershell
dotnet build src\cslightdbgen.sqlitegen\cslightdbgen.sqlitegen.csproj
```

Inspect output for **any `RS20xx` warnings**, not merely a successful exit
code. Release-tracking diagnostics are warnings; an empty unshipped ledger
alone cannot prove that rules were transferred correctly.

## Qualify and Publish

1. Review the release commit and require the normal `Tests` matrix on `main`
   to pass. It uses current Ubuntu, Windows, and macOS hosted images and
   .NET 11/10 SDKs. Workflow, script, props, ledger, and test changes are
   included in its path filters.
2. Create an annotated **`v<version>`** tag on the release commit in `main`,
   then push that tag with explicit publishing authorization.
3. Optionally run the non-publishing validation first. For maintainers:

   ```powershell
   gh workflow run nuget-generator.yaml --ref main -f version=<version> -f tag=v<version>
   ```

   Wait for exact-source CI and all package-consumer jobs to pass. This does
   not reserve a version or publish an archive; the real release run makes
   and qualifies its own candidate.
4. Create and publish a GitHub Release for that existing tag. Publishing the
   GitHub Release starts the release workflow; a tag push alone does not.
   Prefer notes that link to the matching changelog section and describe
   NuGet publication as pending until the workflow succeeds.
5. Review the candidate's full commit and SHA-512 in the pack job summary
   and the six package-consumer results before approving `publish-nuget`
   in the protected environment.
6. Confirm the workflow succeeds and the exact version appears on NuGet.
   Record the source SHA and candidate hash in the release notes. The
   artifact is retained for 90 days; archive it for longer-term evidence
   if needed.

The workflow serializes runs for the same tag without cancelling an active
release. Manual validation can never publish, even if dispatched at a tag.

## One Qualified Artifact

After reusable exact-source CI passes on all three operating systems, the
pack job restores, builds once with the release identity, and packs once
with `--no-build`. It uploads an immutable artifact containing:

```text
cslightdbgen.sqlitegen.<version>.nupkg
package.sha512
```

Package validation checks the nuspec ID, version, repository URL and commit;
the required analyzer DLL, README and license; absence of runtime assets,
dependency assemblies and NuGet dependencies; and assembly/file versions.

The package-consumer matrix downloads that artifact by ID and verifies its
SHA-512 before running all existing SQLite integration tests on .NET 11/10
on Ubuntu, Windows, and macOS. The script pins the compiler host to .NET 11
for Roslyn 5.9.0, including when targeting the .NET 10 runtime. It copies the
integration and A/B host projects into a fresh runner-temporary directory and substitutes an exact
`PackageReference` in all three projects. Source mappings admit the analyzer
only from the local candidate directory, with a separate package cache.
This exercises actual NuGet consumption, independently hosted analyzers,
and generated runtime behavior rather than only project references.

The publisher downloads the same artifact and checks its hash and identity
again. **It never rebuilds or repacks.** There is no duplicate-skipping push
that could hide a conflicting published version.

## Failure and Retry

If source tests, packing, or consumer qualification fail, nothing is pushed.
Investigate the error; never move a published release tag to different source.
A source fix needs a new release commit, version, and tag.

If environment configuration or NuGet publication fails before the package is
accepted, correct the external problem and **re-run failed jobs** in the
original release run. This reuses the already qualified artifact. Do not
re-run all jobs to rebuild it or dispatch a second publishing run.

If NuGet already contains the version, stop and inspect the published package.
Do not overwrite it, delete/recreate the tag, or treat a duplicate as success.
The workflow deliberately rejects that state; it has no automatic
already-published recovery path.

Reruns use the workflow at the original release tag. Retain `GINOC_NUGET`
only while outstanding releases tagged with the old API-key workflow still
need it, then revoke that key and delete the secret. Never move an existing
release tag to adopt trusted publishing.

The November 12 [local qualification record](./release-2026.1112.1700.md)
describes the earlier clock-based process and is historical evidence only.
This document supersedes its publication instructions.
