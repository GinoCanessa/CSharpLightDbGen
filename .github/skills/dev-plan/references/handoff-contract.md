# Owner-Sealed Handoff Contract

**Version: `handoff-v1`. Installation alone activates nothing.** This
reference specifies evidence, not a new skill, artifact owner, or runtime.
An owner must explicitly implement this version before producing or
consuming it. Until the coordinated owner/consumer activation, continue
using the installed workers' existing contracts. Do not retrofit receipts
into a live slot merely because this file has been installed.

Read the target's `AGENTS.md` for conventions and sanctioned commands;
fall back to `README.md` / `CONTRIBUTING.md` only when it is absent, and
state which source you used. Never invent a build, test, or lint command.
Use available local hashing and Git inspection tools. If a required
observation, parser, hash, or history traversal is unavailable, stop.

## Authority and Durable Homes

**Keep domain decisions with their owners.** This document fixes the wire
format and shared validation, not a second interpretation of readiness.
The following links name existing, navigable owner contracts.

| Result kind | Writer and domain contract | Home |
|-|-|-|
| `source` | `dev-request` [Report Format](../../dev-request/SKILL.md#report-format) or `dev-report` [Report Format](../../dev-report/SKILL.md#report-format) | `featurerequest.md` or `bugreport.md` |
| `selection` | `dev-approach` [Judgment File Format](../../dev-approach/SKILL.md#judgment-file-format) and [User Override](../../dev-approach/SKILL.md#user-override) | `approach.md` |
| `plan-work`, `review-plan` | `dev-plan` [Plan Format](../SKILL.md#plan-format) and [Iteration Mode](../SKILL.md#iteration-mode) | `plan.md` |
| `phase-checkpoint`, `final-verification` | `dev-do` [Workflow](../../dev-do/SKILL.md#workflow) and [Iteration Mode (Recovery Path)](../../dev-do/SKILL.md#iteration-mode-recovery-path) | `plan.md` |
| `review` | `dev-review` [Report Format](../../dev-review/SKILL.md#report-format) | `review.md` |
| No result or receipt | `dev-complete` [What This Skill Owns](../../dev-complete/SKILL.md#what-this-skill-owns) | None |

Put receipts under one current `## Handoff Evidence` in the existing
owner artifact. Do not create sidecars, a conductor registry, or a Git
metadata store for these receipts. Candidate authors still write only
their assigned document. The approach owner hashes their outputs and
records their fingerprints in `approach.md`; authors do not write seals.
The judge remains read/search-only, and reviewers acquire no edit tool.
An implementer consumes work identities but never edits or seals a plan.

The owner skill remains canonical for its domain states, required
sections, eligibility, and approvals. A valid envelope around an invalid
domain result authorizes nothing. Do not replace the owner's full
validator with a heading, status ordering, or a file-existence test.
An owner without a v1 domain contract cannot issue a consumable v1 seal.

**Keep evidence public-safe.** Source and plan artifacts can be
published. All protocol tables, including those in private artifacts,
contain only identifiers, digests, repository-relative paths, disposition
codes, and Git identities. Never copy candidate/reviewer prose, command
output, credentials, remote URLs, or absolute machine paths into them.
Keep explanations in their owning artifact; reference a public-safe ID
or digest instead of transplanting private text. These receipts neither
replace nor grant authority under publishers' private ownership receipts.

## Bytes, Tables, and Structural Parsing

**Parse before projecting; never hash a Markdown rendering.** Read strict
UTF-8 bytes and retain byte offsets. Reject invalid UTF-8. A leading UTF-8
BOM is allowed and remains operative bytes; ignore it only for locating
the first title. Recognize CRLF, LF, and an unterminated last line without
converting any of them. Do not trim content, normalize Unicode, fold case,
or add a final newline when computing an identity.

Use CommonMark 0.31.2 block boundaries for fences, block quotations
(including lazy continuations), indented code, and list nesting.
Recognize delimiter, heading, and metadata controls only at the document
root. Decision-ledger and journal records are the explicit exception:
they are whole unindented root list items, never nested list content.
None is recognized inside a fence, quotation, or indented code block.
Backtick and tilde fences both count; their closing character and minimum
length must match the opener. A quoted or fenced control example is
ordinary operative content, not authority and not an exclusion. Refuse
an unclosed fence or a boundary that cannot be established reliably.
No Markdown library or runtime dependency is required by this document.

Protocol delimiters occupy a whole root-level line, in column zero:

```text
<!-- handoff-v1:receipt begin -->
<!-- handoff-v1:receipt end -->
<!-- handoff-v1:control begin -->
<!-- handoff-v1:control end -->
<!-- handoff-v1:execution begin -->
<!-- handoff-v1:execution end -->
<!-- handoff-v1:history begin -->
<!-- handoff-v1:history body -->
<!-- handoff-v1:history end -->
```

The first three block types contain exactly one table, with this header
and separator, followed immediately by their specified rows:

```markdown
| key | value |
|-|-|
```

A row is exactly `| <key> | <value> |` on one physical line. Keys are the
lower-case spellings specified here, exactly once and in order. Values
are compact JSON **strings or recursively nested arrays of strings**;
numbers, booleans, nulls, objects, duplicate/extra rows, comments, and
blank rows are invalid. All integer values are decimal strings without
leading zeroes; positive integers exclude `"0"`. A required empty list is
`[]`, never an omitted field. `"none"` is a sentinel only where allowed.

Use one canonical JSON spelling: no whitespace outside strings; escape
quote as `\"`, backslash as `\\`, LF as `\n`, CR as `\r`, and tab as `\t`;
escape other U+0000 through U+001F as lower-case `\u00xx`, and pipe as
`\u007c`. Emit other Unicode scalar values literally in UTF-8, not as
escapes. Reject unpaired surrogates and alternate escape spellings.
This makes table parsing unambiguous; decoded values, not JSON quotes or
Markdown padding, enter the envelope serialization below.

Recognize the reserved root-level prefix `<!-- handoff-` before treating
HTML comments as ordinary Markdown. An unknown version, misspelled marker,
unmatched end, truncated table, or nested protocol block is an error,
never content to silently discard. The only nesting exception is a
validated historical `control` inside a history body. Protocol-looking
text inside fences or quotations remains bytes, as above.

Require one title, one current owner metadata table, and at most one of
each owner-defined singleton section. The metadata table has the existing
`| | |` / `|-|-|` header, root-level `| Key | Value |` rows, and no
duplicate keys. It lies after the title and before the first root-level
`##` section. Parse the owner's required keys and value grammar;
unknown metadata rows remain operative. A second current metadata table,
conflicting current selection/override, or duplicate phase number
refuses. Historical and example headings cannot satisfy a current
requirement. Missing required owner fields also refuse.

**Do not let parse errors become hash exclusions.** Finish lexical,
schema, ownership, supersession, and independent control validation before
using any projection as authority. First identify structurally valid
candidate exclusion spans; compute a provisional projection; then verify
the content/result/seal hashes and all control gates before accepting
that projection. This order avoids a circular requirement to verify a
receipt's content hash before identifying its own excluded span.
Report the artifact, block/key or byte span, failure, and responsible
owner. There is no last-record-wins recovery.

## Receipt Envelope and Identities

Use these required keys in this exact order. The value types in this
table describe decoded values, not literal placeholder receipts.

| Key | Value |
|-|-|
| `version` | `"handoff-v1"` |
| `kind` | One result kind from the ownership table |
| `subject` | The subject spelling below |
| `revision` | Positive integer string, per kind/subject |
| `supersedes` | Previous seal identity, or `"none"` for revision 1 |
| `result-identity` | `r-sha256:` followed by 64 lower-case hex digits |
| `content-digest` | `sha256:` followed by 64 lower-case hex digits |
| `dependencies` | Dependency rows defined below |
| `context` | The eight-element context tuple below |
| `approval` | The six-element approval tuple below |
| `evidence-basis` | The five-element basis tuple below |
| `seal-identity` | `s-sha256:` followed by 64 lower-case hex digits |

For `source`, `selection`, `plan-work`, `review`, and
`final-verification`, the subject is the artifact's literal
repository-relative path. A `review-plan` subject is that plan path plus
`#review/<k>`; a `phase-checkpoint` subject is that plan path plus
`#phase/<n>`. Here `k` and `n` are positive integer strings. The suffix is
a subject identifier, not part of a filesystem path. Review receipt
revisions are not review-iteration counters.

Use `/` separators and exact filesystem/Git path spelling for logical
paths. Reject absolute paths, empty segments, `.` / `..`, backslashes,
NUL, CR, LF, and paths escaping the captured root. Require strict UTF-8
names. Do not lowercase or Unicode-normalize them. A subject's path
cannot contain `#`; an unrepresentable path requires owner resolution,
not a lossy alias. A digest's lower-case representation does not imply
case folding of the bytes being hashed.

Other public-safe IDs (boundary, role, finding, and check IDs) use
`[A-Za-z0-9][A-Za-z0-9._/-]*`. Disposition, authority, and limit codes
use `[a-z][a-z0-9-]*`; their permitted meanings belong to their domain
owner. An unknown code cannot imply success. Use role names for role
IDs and `<stage>/review/<k>` for an iteration-qualified stage ID, not
private prose. Fixed states, paths, OIDs, and the typed identity prefixes
have their explicitly specified grammars instead.

### One serialization and three identity domains

Let `U(s)` be the exact UTF-8 encoding of decoded string `s`. Define:

```text
S(string) = ASCII("s" + decimal(byte-length(U(string))) + ":") || U(string)
S(list)   = ASCII("l" + decimal(element-count(list)) + ":")
            || S(element 1) || ... || S(element n)
H(bytes)  = lower-case hexadecimal SHA-256(bytes)
D(bytes)  = "sha256:" || H(bytes)
```

There are no separators, terminators, BOMs, or newlines added by `S`.
`S("")` is `s0:`; `S([])` is `l0:`. Lengths count **UTF-8 bytes**, not
UTF-16 code units or rendered characters. List order is significant.
Sort set-like path lists by unsigned lexicographic UTF-8 bytes, not locale
collation. Sort manifest rows by exact logical path, then by their
specified secondary key; sort dependencies by `(kind, subject)` using
the same comparison. Reject duplicates and unsorted serialized sets.

Let `P` be the operative byte projection below, `B` the evidence-basis
tuple, and `C` the context tuple. The operative payload is `B[4]` and the
stable context is the list `[C[0], ..., C[5]]` (zero-based indices).

```text
content-digest = D(P)
result-identity = "r-sha256:" || H(S([
  "handoff-v1", "result", kind, subject, content-digest,
  dependencies, [C[0], C[1], C[2], C[3], C[4], C[5]], B[4]
]))
seal-identity = "s-sha256:" || H(S([
  "handoff-v1", "seal",
  [[key, value], ...]  // every envelope row in order except seal-identity
]))
```

The comment is explanatory, not serialized. Include `version` in the
seal's row list even though its domain prefix also names the version.
The seal covers the completed envelope, including its result ID,
revision, predecessor, observations, basis, and approval. It omits
**only** its own row. The result omits approval, revision, supersession,
and non-operative basis/observation data. Confirming identical work can
therefore change its seal without changing the subject of confirmation.

A plan-work result ID **is the workset ID**. A phase ID has its own
`p-sha256:` domain defined below. A journal `receipt: <id>` always names
a **seal**, not a result, workset, or phase ID. These are integrity
identities, not signatures or evidence of who had access to a file.
Keep a derived result's own ID in its excluded receipt/approval/runtime
records, not in its operative prose or control data. An upstream ID is
a dependency; embedding the result being computed in its own projection
creates a forbidden identity cycle.

### Supersession and preservation

For each kind/subject, require a single chain beginning at revision 1
with `supersedes: "none"`. Revision `j + 1` must name the seal at `j`
in that same chain. Require exactly one tip. Preserve every earlier
receipt byte-for-byte; re-emit them when an owner rewrites its artifact.
Never renumber, replace, delete, or silently repair a sealed receipt.

Reject duplicate revisions or blocks (even identical copies), two roots,
forks, cycles, a missing predecessor, a cross-subject predecessor, and
unknown versions. Validate every seal in the chain, not just the tip.
Multiple revisions may legitimately carry the same result ID, for
example after a confirmation; that is not two active results.

Only the tip can authorize **current** consumption. Historical seals
can prove frozen facts, not authorize new work. A torn prospective receipt
does not replace the earlier tip, but its malformed control still blocks
consumption until its owner reconciles it. Do not hide it in an excluded
section and proceed. Legacy revalidation starts a truthful revision-1
chain; it does not manufacture predecessor receipts.

Recompute a current tip's content digest from the current operative
bytes. For a historical receipt, recompute its result from its recorded
content digest/payload/context and validate its seal; do not pretend a
missing old body was rehashed. Rehash preserved historical bytes when
available. If the requested purpose needs old authored content that is
unavailable, stop. Frozen review scope or commit proof does not require
copying the old private report into the consuming plan.

### Dependencies

Each dependency row is:

```text
[kind, subject, use, result-identity, content-digest]
```

`use` is `current`, `frozen`, or `not-used`. Resolve a current dependency
to its owner's active tip with exactly that result ID and content digest;
validate that owner's domain, approval, and context for the requested
purpose. A newer seal of the same result does not itself stale a
dependent result, but its current approval and pending questions matter.

A frozen dependency resolves to a preserved, valid historical receipt
with exactly those identities. Check its concrete historical evidence
for the purpose in question; do not compare an old review's body with a
new report or call it a review of the new tree. Frozen references are
permitted only for reviewed snapshots, the approved workset named by a
review/planning/checkpoint record, and proven completed prefixes.
Pending execution still requires that workset to be current. A frozen
edge cannot freeze an obsolete source or selection to authorize pending
implementation.

`not-used` is permitted only for a standalone plan's `selection`
dependency: subject is the expected sibling `approach.md` path, and both
identity fields are `"none"`. Establish that no approach artifact exists
and no prior plan depended on one. A lone candidate, skeleton, rejection,
or deleted prior selection is not `not-used`. Orchestrated planning
cannot use it.

Require unique `(kind, subject)` pairs and an acyclic dependency graph.
Graph nodes are `(kind, subject, result-identity)`, not merely artifact
paths: a later workset may depend on a review of an earlier workset.
Resolve all required edges; a missing receipt is not an empty dependency.
Do not include a result's own identity, its future checkpoint, or its
review-plan receipt in its dependencies. In particular, remediation
plan-work can depend on the frozen review; review-plan then depends on
that workset, never the reverse.

These are the required direct edges; use manifests for ordinary files,
not extra invented result kinds:

| Kind | Dependencies |
|-|-|
| `source` | `[]` |
| `selection` | Exactly one current source |
| `plan-work` | One current source; one current selection or the permitted not-used edge; the frozen review for a review-driven revision; every checkpoint in inherited-prefix as frozen |
| `review` | The frozen workset reviewed for plan-slot scope; none for an ad-hoc scope without a workset |
| `review-plan` | Exactly one frozen review and one frozen workset matching its payload |
| `phase-checkpoint` | Exactly one frozen workset, current and authorized when executed; legacy bootstrap instead uses the explicitly provisional present-time workset below |
| `final-verification` | One current workset and the frozen checkpoint for every completed phase |

Subjects and paths must agree with the actual source, selection, plan,
and scope, not merely with filenames that happen to exist. At emission,
a review-plan's referenced workset must be the current one; before
executing any mapped pending work, require it still to be current or
obtain a superseding planning receipt. Its frozen edge lets completed
earlier iterations remain facts after later worksets are appended.

When traversing a frozen historical result, resolve its own predecessor
edges against the identities it recorded, not today's active tips.
Validate the historical objects and approval actually needed then.
This does not make those predecessors current inputs for new work.
If a historical approval or required fact cannot be established, stop.

## Operative Projections and Controls

**Retain all bytes not explicitly excluded.** Collect validated excluded
byte intervals, require them to be disjoint, then concatenate the
remaining byte spans in original document order. An excluded line
includes its actual line terminator; an excluded block includes its
delimiters and the end delimiter's terminator. Do not consume adjacent
blank lines. An insertion of a receipt must not add padding outside its
block if it is intended to be progress-only.

The only exclusions are:

1. Fully validated receipt blocks in the proper `Handoff Evidence`
   section, including preserved historical receipts.
2. The current metadata table's `Created` and `Last updated` rows, when
   present with valid `YYYY-MM-DD` calendar dates. No other timestamp
   or timestamp-looking prose is excluded.
3. That table's valid `Issue` row under `dev-issue`
   [The Issue Binding](../../dev-issue/SKILL.md#the-issue-binding).
4. That table's valid `Status` row and a plan phase's single
   `**Status:** <state>` line. Only owner-defined states are valid.
   A Blocked state may carry the owner's one-line reason after
   ` — `; arbitrary suffixes or an unknown state refuse.
5. Valid single-line `ASSUMPTION`, `USER`, `QUESTION`, and `HANDBACK`
   entries in their designated section, using `dev-complete`
   [The Standing Directive][standing-directive].
   Preserve their ownership, stage/iteration, and question-step grammar.
6. The plan's single, fully validated `## Progress Log` region, from its
   heading through the byte before the next root-level `##` heading
   (or EOF), using the decoder below.
7. Valid typed `execution` and explicitly superseded `history` blocks
   defined below, never an arbitrary prose region with that name.

Neither `## Notes` nor `## Handoff Evidence` is excluded as a section.
Their headings, blank lines, custom prose, links, examples, and unknown
authored content remain operative. A line that merely resembles a
decision record is not discarded: a malformed recognized prefix refuses,
and ordinary non-record prose remains hashed. Settled decisions belong
in operative prose as well as the ledger. Runtime annotations cannot
introduce steps, change ownership, or waive verification.
A root record/control in the wrong section refuses rather than becoming
an alternative current control or an exclusion. Use a fence or quotation
for examples, whose bytes stay operative.

[standing-directive]: ../../dev-complete/SKILL.md#the-standing-directive

| Projection | Retained content after the applicable exclusions |
|-|-|
| Source | The whole request/report, including evidence, hypotheses, custom sections, and free-form Notes |
| Candidate | The complete author document, not just Shape or What Changes; only its valid Issue/Created/Last updated metadata rows can be excluded |
| Selection | The current manifest/control, judgment, override, and every other non-excluded authored byte |
| Plan work | All scope, dependencies expressed as prose, global constraints, every phase definition, ownership, verification, and other operative prose |
| Review | The resolved scope/control and whole current report, including its recommendations and limitations |

Candidates contain no receipts, execution/history blocks, or current
status/decision-ledger controls of their own. Such a block in a candidate
refuses rather than opening a new author write surface. Their ordinary
prose and quoted/fenced examples are retained.

For `review-plan`, `phase-checkpoint`, and `final-verification`, `P` is
not the mutable host plan. It is exactly
`S(["handoff-v1","payload",kind,B[4]])`. Those typed results attest to
their payload and dependencies, not to having authored the plan.

### Current selection and review-scope controls

Use a `control` block with keys `version`, `kind`, `subject`, `data`,
in that order. Version is `"handoff-v1"` and subject is the host artifact
path. Unlike receipts, **retain the entire block in the projection**.
There is exactly one current `selection` control in `approach.md`, and
one current `review-scope` control in a sealed `review.md`.

A selection's `data` tuple is:

```text
[decision-revision, mode, outcome, judged-selection, candidates, override]
candidates row =
  [candidate-id, path, activity, fingerprint, last-owned-fingerprint,
   eligibility]
override = ["none"]
        or ["user", decision-revision, candidate-id, fingerprint,
            user-record-digest]
```

The approach owner defines the valid mode, outcome, activity, and
eligibility codes and the required judgment/rejection prose. Sort rows
by candidate ID; IDs and paths are unique. Fingerprints use the complete
candidate projection. A missing owned fingerprint is `"none"`, which
confers no overwrite permission. The current metadata, control, visible
judgment, and any override must agree. The current override binds this
decision revision and an active candidate's current fingerprint; its
digest names the actual owner-recorded USER line, never an assumption.
Preserve its reason in the visible override, not just a digest.
That provenance line names the decision revision and candidate, not the
new effective result ID: putting the resulting ID into an operative
override would create a hash cycle. A required confirmation of the
effective result is a separate approval record.

The selection payload is the ordered list of
`[candidate-id, path, fingerprint]` for exactly the active candidates.
The owner rehashes **every** active file, checks completeness and
eligibility, and compares the control and payload before sealing and
consuming. Enforce the owner's `collapsed -> {A}` and
`contested -> {A, B, C}` cardinality, not the number of files found.
Inactive files cannot enter through directory enumeration.
An invalid override refuses; it does not restore the judge's winner.
Changing the judgment increments decision-revision; resealing only an
approval does not. Rejection remains a completed non-selectable result.
Only an eligible active comparative loser may override a selected
result; no override can waive disqualification or all-candidate rejection.
When collapsing, preserve inactive files and both their observed and last
owned fingerprints. Re-expansion must reconcile them before activation;
changed or missing provenance never authorizes automatic overwriting.

A review-scope control's `data` is exactly the scope descriptor defined
under Context. Its bytes, the report's visible Scope, and the receipt's
scope must agree. The review owner defines eligibility and scoring; all
dispatched reviewers and synthesis receive that same resolved descriptor.

### Typed pending execution evidence

Use an `execution` block only in a plan's `Handoff Evidence`, with keys
`version`, `type`, `subject`, `data`, `identity`. Version is
`"handoff-v1"`, type is `"pending-phase"`, subject is
`<plan-path>#phase/<n>`, and:

```text
data = [workset-id, phase-id, base-commit, staged-tree, changed-paths,
        checks]
identity = "e-sha256:" || H(S([
  "handoff-v1", "execution", type, subject, data
]))
```

Use the check-result rows below. All checks must pass before this
pre-commit record is written. It accompanies, but never replaces or
extends, the existing PENDING line. Require the same base, tree, and
exact changed-path set in both. Keep the typed record after PENDING is
replaced so the checkpoint can independently reference the tuple.
Once a COMMIT exists, validate the tuple against that actual object.
This record proves neither a commit nor phase completion.

There is one such record per successful phase commit attempt. If an
attempt's tuple must change, do not rewrite an existing PENDING record
or pretend it was never written; stop at execution recovery. Verification
failures and ordinary runtime commentary use NOTE, not new untyped
exclusions. No execution block is permission to add work.

### Explicitly superseded history

A `history` block begins with a table whose keys are `version`, `kind`,
`subject`, `superseded-seal`, `successor-revision`, `body-digest`, in
that order, followed immediately by the `history body` delimiter.
Version is `"handoff-v1"`. Kind is `source`, `judgment`, `override`, or
`review`; subject is that owner's artifact path. The body extends from
after the body delimiter's terminator to before the end delimiter.
Hash those raw body bytes for `body-digest`.

The predecessor seal must exist in that owner's receipt chain and the
successor revision must explicitly supersede it. For the only legacy
exception, use `"legacy-unsealed"` as the predecessor, successor `"1"`,
and a present-time-revalidation receipt that records the legacy limit.
This is preservation of unproven history, not historical certification.
Judgment and override history refer to the selection receipt chain.

Move superseded authored spans byte-for-byte, with their provenance;
do not paraphrase them into purported originals. An archive is historical
only after its header, body digest, ownership, and successor validate.
Do not nest receipts, execution, or history blocks. Validate any archived
control table but never make it current. An unknown/malformed recognized
control cannot be laundered by wrapping it in history. Keep unsealed
failure text as quoted operative evidence during owner reconciliation
instead. Never archive completed phase definitions to remove them from
the current plan.

## Context and Observation Boundaries

The context tuple is ordered as follows:

```text
C = [location, anchor, conventions, inputs, loaded, scope, observed,
     progression]
location = [repository-id, worktree-id, ref-kind, ref-name, object-format]
anchor = [commit-oid, tree-oid]
observed = [location, commit-oid, tree-oid, index-tree-oid, manifest]
```

The first six fields form stable context. `observed` is the actual
sealing observation, not an assertion that HEAD never advances.
`progression` is an ordered phase-commit proof from anchor to that
observation. Current consumption takes a fresh observation and verifies
its own progression; it does not edit the receipt to record a read.

Resolve locations consistently using Git's absolute-path outputs:
common Git directory, this worktree's Git directory, and worktree root.
Remove only the command's terminal line ending from each returned path;
do not case-fold, rewrite separators, trim spaces, or substitute a remote.
Use these same read-only queries from the captured worktree on every
observation; require each to succeed:

```text
git rev-parse --path-format=absolute --git-common-dir
git rev-parse --absolute-git-dir
git rev-parse --path-format=absolute --show-toplevel
```

```text
repository-id = D(S(["handoff-v1","repository",absolute-common-git-dir]))
worktree-id = D(S([
  "handoff-v1","worktree",absolute-worktree-git-dir,absolute-worktree-root
]))
```

Store only the resulting opaque digests, never those absolute strings.
A moved checkout or differently resolved location conservatively
mismatches. A linked worktree shares the repository ID but not the
worktree ID. `ref-kind` is `symbolic` or `detached`; use the full symbolic
ref, not its short branch name, or `"none"` for a detached ref.
Branch identity is compared even when commit and tree OIDs are equal.

Read Git's object format and require full OIDs of the length that format
specifies, with verified commit/tree object types. Do not assume a hash
length, peel tags, substitute a branch expression, or accept a truncated
traversal. Capture the actual index tree without altering the index.
Unmerged entries, an unborn HEAD, unavailable objects, or an unsupported
object format prevent a consumable v1 context; do not synthesize OIDs.

`conventions` lists the actual convention files used, including the
fallback files when applicable. `loaded` lists every loaded skill,
active role definition, and support reference, including this reference.
Both are sorted unique paths present in `inputs`. Fingerprint them even
when an exclude installation hides all of them from Git status.

### Input manifests

Each `inputs` / observation manifest row has this exact shape:

```text
[path, roles, policy, tracking, head-view, index-view, working-view]
roles = sorted nonempty subset of ["convention","input","instruction",
                                 "scope"]
policy = "stable" or "phase-evolving"
tracking = "tracked" or "untracked" or "ignored"
view = [presence, git-mode, object-oid, bytes-digest]
```

Sort rows by exact path. A present Git view uses presence `"file"` or
`"symlink"`, its actual mode and blob OID, and `D` of raw blob bytes.
A present working-copy view uses the same presence/mode vocabulary,
`"none"` for object OID, and `D` of the actual working bytes. Hash a
symlink's link text, not its referent; a consulted referent needs its own
row. File modes are `"100644"` or `"100755"` and symlink mode is
`"120000"`; capture working mode using Git's applicable file-mode
semantics, not a guessed platform default. Absence is exactly
`["absent","none","none","none"]`.

`tracked` means present in that observation's HEAD or index, including
deletions (the production anchor for the stable inputs manifest);
otherwise use `ignored` if Git ignores the path and `untracked` if not.
The three separate views distinguish staged content from working content
and tracked deletion from a never-created input. Do not encode deletion
as the hash of an empty file. Record absent expected paths too, so their
appearance is detectable. Refuse unsupported file modes, unreadable
content, unresolved links, or relevant submodule/external inputs that
cannot be represented by this rooted manifest; do not treat them as
unchanged regular files.

The manifest is an explicit relevant-input inventory, not a claim that
Git status enumerates all inputs. Include discovered configuration,
consulted ignored files, and the paths needed to evaluate the declared
scope. Re-enumerate that scope on recheck to detect additions/removals.
Never leave a newly relevant path outside the inventory merely to make
a comparison pass. Unrelated dirty state is still subject to the
executor's independent whole-tree and owned-path gates.

Represent source/selection/work dependencies by their result projections
and dependency rows, **not duplicate raw-file hashes in this manifest**.
Otherwise journal/status progress would stale their consumers. Their
excluded controls are still validated independently. Do not put a
result's own output in its stable input manifest. A refinement that reads
prior output captures that prior result/fingerprint before editing and
preserves it as provenance, rather than making the new result depend on
itself. Generated candidates become inputs to the judge only after the
owner has captured them at that next dispatch boundary.

Use `stable` for conventions, loaded instructions, and inputs not
explicitly allowed to advance. `phase-evolving` is allowed only for
repository content eligible to advance through this slot's verified
owned phase commits. A source/selection owner may mark inspected code
this way before a workset exists; advancement is conditional on a later
approved workset whose dependency chain contains that exact result.
Another slot's commits cannot supply that progression. It never applies
to a source, candidate, selection, plan definition, convention, or loaded
instruction. No policy code lets a dirty working copy count as a
committed advance.

### Capture, delegate, recheck

Capture the relevant input bytes, all context fields, dependency
identities, and pending/approval controls **before** work or dispatch.
Keep that observation in the owner's continuation until it can be
written within the owner's ordinary write boundary.

Require each delegated role to return the paths it actually consulted,
including loaded instructions and support references. Give it paths,
not pasted file contents. The owner performs all tool-based hashing;
an author's or judge's model-generated digest is never evidence.
Hashing does not grant either role a shell tool.

Recheck before sealing and before a consumer acts. A read-only production
step must observe identical relevant inputs at its two boundaries.
Execution output changes instead require the approved-work and exact
commit proof below. After writing a receipt, read it back and recompute
its projection and seal, then recheck inputs again. An unstable set
remains unsealed/unusable; a partial write is not success.

**A late-discovered input is not a historical observation.** If any
consulted relevant path lacks a pre-read capture, do not hash its final
contents and backdate the result. Leave the result unsealed, capture the
expanded set, and have its owner revalidate the affected result against
that set. It may need another author/judge/reviewer pass, retaining
isolation and any applicable review budget. Stop if the boundary cannot
be established. This is not permission for routine healthy-owner renewal.

### Resolved review scope

Use `["none"]` for non-review scope. A review scope is:

```text
[scope-kind, baseline, targets, commits, path-filter, exclusions,
 scope-manifest, focus-digest, measurement]
target = [view, commit-oid, tree-oid]
scope-manifest row = [path, view, file-view]
exclusions row = [path, disposition-code]
measurement = [basis, file-count, line-count]
```

`scope-kind` is `snapshot` or `change`. For snapshots, baseline is
`"none"` and commits is `[]`. A change uses a full baseline commit OID,
or `"per-commit"` for a noncontiguous exact commit set; verify each
commit's actual parent(s) in that case. Resolve symbolic inputs before
dispatch. A current-file audit without a baseline is a snapshot; a
file-limited diff is a change.

`view` is a full commit OID, `"index"`, or `"working-copy"`; a target's
commit/tree identifies its immutable commit anchor even for a working
snapshot. `file-view` uses the manifest view tuple above. Include every
version actually inspected: baseline/target blobs for a diff, and the
captured file contents for a snapshot. Sort targets by view, commit sets
by full OID, and scope rows by `(path, view)`. Keep path-filter and
exclusions explicit; an empty filter means no path restriction, not an
empty scope. Reject duplicate rows.

`focus-digest` is `D` of the exact UTF-8 focus text (empty text when none);
do not copy private focus prose into a plan receipt. Measurement basis
is `snapshot-lines`, `changed-lines`, or `full`. Counts are nonnegative
integer strings; `full` uses `"none"` for both and retains mandatory
partitioning. Snapshot counts measure distinct files and lines actually
inspected, never a diff of unchanged files. Count LF-terminated lines
plus a nonempty final unterminated line. If lines cannot be measured,
do not claim a below-threshold scope. The owner's existing numeric
threshold and independent reviewer dispatch remain unchanged.

All roles use the same eligibility semantics. A demonstrated older
defect is eligible in a snapshot. In a change, an unworsened old defect
is out of remit, not false or zero-confidence solely due to age.
Scope drift prevents a mixed-snapshot seal. A frozen descriptor identifies
what was reviewed, never later fixes.

## Basis, Payloads, and Approval

The evidence-basis tuple is:

```text
[basis, prior-result, limits, observations, payload]
observation row = [boundary-id, role-id, before, after, consulted-paths]
before / after = [observed-context, dependencies, candidates, controls]
controls row = [artifact-path, control, key, value, record-digest]
check-result row = [check-id, definition-digest, outcome, evidence-digest]
```

`basis` is `production` or `present-time-revalidation`. For production,
prior-result is `"none"`; for revalidation it is the prior result ID or
`"legacy-unsealed"`. Limits is a sorted list of public-safe codes;
`legacy-inputs-unproven` records the legacy case. A limit that prevents a
required check from passing prevents consumption; a code is not a waiver.
Use no arbitrary prose in protocol tuples.

Keep observations in actual boundary order, with unique boundary IDs.
Observed-context uses the full `observed` tuple from Context.
Dependencies use dependency rows; candidates use active
`[candidate-id, path, fingerprint]` rows. Thus both boundary snapshots
record which projected artifacts were consumed, without raw hashes that
would invalidate on progress. Compare them before and after, not merely
the final files. A prior output read for refinement is an explicitly
declared input/output transition: capture its prior receipt/fingerprint
before the write, and check its new output at the next boundary. It is
not an unexplained change to a read-only dependency.

Consulted paths are sorted and must be covered by that boundary's before
manifest or dependency/candidate capture. Include at least one owner
boundary, plus each delegated boundary; production and revalidation both
need real observations. Do not retrospectively remove an input from a
boundary to make it pass. For an unsealed prior output, retain its
tool-computed projection digest in the before candidate row (candidate
refinement) or a before dependency row with result `"legacy-unsealed"`
(owner revalidation only). That observation grants no dependency/reuse
authority; the final envelope still requires valid dependency rows.

Control snapshots retain only these public-safe forms, sorted by
`(artifact-path, control, key)`:

- `issue`: key `"binding"`, value `"not-published"` or `"#<N>"`.
- `status`: key `"artifact"` or `"phase/<n>"`, value the parsed known
  domain state; omit a field only where that artifact has no such field.
- `decision`: key `"<tag>/<ordinal>"`, where tag is `ASSUMPTION`, `USER`,
  `QUESTION`, or `HANDBACK` and ordinal is its positive document-order
  position among decision records; value is its owner stage/iteration ID.

Record-digest is `D` of that complete raw metadata/status/decision line
including its terminator. Do not put the question, answer, blocked
reason, Issue URL, or other private prose into the snapshot.
Before/after snapshots cover the consulted artifacts and the owner's
existing output controls, if any. Capture them before approval/sealing;
these observations are outside the operative result hash.

Validate excluded controls independently on every consumption. Missing
or unknown states refuse; pending questions return to their recorded
owner/step; approval records must still apply. Compare Issue bindings
with the captured and preserved prior bindings: a downgrade or different
bound number requires `dev-issue` reconciliation under its ratchet.
One bound artifact and one unbound artifact is missing back-fill, not
permission for another owner to invent a value. Do not perform a GitHub
write, or claim that equal content hashes settle a binding disagreement.

For source/review readiness, require the active owner's final observation
to record the same ready state now being consumed. A manual Draft-to-ready
status edit cannot upgrade an earlier draft seal. New plan-work is
sealed in the owner's ready state. Present-time plan revalidation may
retain In-progress, Blocked, or Complete only after independently proving
the existing execution state; never reset it to ready to avoid recovery.
Later executor status transitions need the phase/recovery/final proofs.
Thus excluded status changes can preserve the work identity without
independently granting new authority.

Check IDs identify the exact verification item in the approved phase or
final gate. Use `phase/<n>/verification/<i>` or `final/<i>`, with `i`
the positive item index in document order. The item's byte span includes
its nested content and terminators up to the next peer item or section
boundary; definition-digest hashes those operative bytes. Keep check
rows in that same order, with no duplicate or omitted required item.
Outcome is `pass`, `fail`, or `unavailable`; evidence-digest hashes the
actual local tool/inspection record, not an invented success message.
Keep logs private/local when necessary. A consumer requires every
mandated check and only `pass` outcomes for completion.

Use these payloads; reject extra or missing elements:

| Kind | Operative payload |
|-|-|
| `source` | `[]`; require the correct source owner and domain validation |
| `selection` | Active candidate rows defined above |
| `plan-work` | `[global-digest, phases, inherited-prefix]`, defined below |
| `review` | `[scope, finding-dispositions]`; scope equals `C[5]` |
| `review-plan` | `[iteration, review-result, scope, dispositions, workset-id, remediation-phases]` |
| `phase-checkpoint` | `[workset-id, phase-number, phase-id, pending-evidence-id, base-commit, staged-tree, changed-paths, commit-oid, checks]` |
| `final-verification` | `[workset-id, completed-prefix, checked-commit, checked-tree, checked-observation-digest, checks]` |

Finding-disposition rows are `[finding-id, severity, disposition-code]`,
sorted by finding ID; keep the owner's severity vocabulary canonical.
A review-plan disposition adds its ordered remediation phase IDs as a
fourth element. Resolve every relevant finding to its actual review
result. No private finding title, recommendation, or reviewer prose is
copied into source/plan evidence.

For review-plan, retain the exact reviewed scope even after `review.md`
is overwritten. Remediation-phases is the ordered list of
`[phase-number, phase-id]`, or `[]` for none. A clean review creates no
phase or commit. Bind planning idempotence to `(iteration, review-result)`;
retrying that pair reuses its phases, never duplicates them. A changed
pending-work mapping requires a superseding review-plan receipt and a
new marker, not a new review pass.

A checkpoint names the retained typed pending record and must match the
actual commit's sole parent, tree, and exact changed paths. The final
checked-observation digest is `D(S(C[6]))`. Prefix rows have the shape
defined below. Require a checkpoint's plan-work dependency and all final
checkpoints to resolve; receipt syntax alone proves no execution.
For a production checkpoint, `C[6]` must record its actual post-commit
HEAD/tree and index tree, all equal to the checked commit/tree as
applicable. Its observed manifest includes every changed path's
post-commit views as well as relevant inputs, even if a changed output
was absent from the stable input manifest. These output observations
are outside the result hash and are sealed as evidence, not circular
input dependencies. Present-time legacy checkpoints use the narrowly
limited form under Recovery, never a fabricated post-commit observation.

### Work-bound approval

The approval tuple is:

```text
[disposition, subject, mode, record-path, record-digest, authority-code]
```

Disposition is `user`, `automatic`, `not-required`, `pending`, or
`refused`. Mode is `interactive`, `automatic`, or `standalone`; record
the actual mode, never infer it from an old artifact. Subject is the
result ID being confirmed. For a selection or plan-work receipt it is
its own result/workset ID. A review-plan with execution work names its
referenced workset; a clean review-plan and other kinds name themselves.
This field is outside the result hash, so none of these bindings cycles.

For `user`, reference the actual USER line in the owner's artifact;
for `automatic`, the actual ASSUMPTION line resolving this allowed
decision; for `pending`, the current QUESTION line. Record-digest is
`D` of the complete raw record line, including its existing terminator.
The record must explicitly name this approval subject, the correct
stage/iteration, and the decision. Record-path is that artifact path.
`refused` names the actual refusal/answer record when durable, or uses
`"none"` for both record fields when writing was forbidden. Never
manufacture a record merely to fill the envelope.

`not-required` uses `"none"` for both record fields and an owner-defined
authority code identifying why no approval applies. It never stands in
for a missing confirmation. Other authority codes identify the owning
decision, not an exception to a safety gate. Validate them under the
owner's policy and current invocation mode for new work. For a frozen,
already-completed fact, validate its actual historical mode and approval
instead; switching modes does not invent consent or require redoing
completed phases.

Compute the result first, present/apply the decision about that exact
result, then finish and hash the envelope. If an answer changes operative
work, recompute the result and obtain any required new confirmation.
Preserve old USER entries as history, not approval of the revision.
A pending or refused receipt can record a result but grants no downstream
action. A review-plan planning-complete seal/marker requires settled
decisions and all required work approval, never `pending` or `refused`.

Pending questions outrank readiness and seals in both modes. Preserve
the Standing Directive's fixed declines, owner-written destinations, and
undurable-record carve-outs. Automatic handling can settle an allowed
preference, not an unexplained failure, invariant violation, missing
historical evidence, or safety refusal. No receipt grants publication.

## Phase Definitions and Worksets

Parse the plan's one root-level `## Phases` section. Its phase headings
are exactly `### Phase <n>: <nonempty name>` in ascending, contiguous
positive-number order. A phase span starts at that heading and ends
immediately before the next phase heading or next root-level `##`
heading. Blank lines and separators before the next heading belong to
the preceding span; preserve them when appending a phase. Reject other
unclassified level-three sections inside Phases.

Each phase must carry the owner's Goal, exhaustive literal Owned paths,
Steps, Verification, and exactly one Status. Apply the ordinary
exclusions to its span to form `P_n`. Define `G` as the full plan
projection with **the entire span of every phase removed**, irrespective
of phase status, but retain the Phases heading and its global preamble.
Thus global scope, final verification, risks, and custom Notes belong
to `G`.

```text
definition-digest(n) = D(P_n)
global-digest = D(G)
phase-id(n) = "p-sha256:" || H(S([
  "handoff-v1", "phase", plan-path, phase-number,
  definition-digest(n), global-digest, dependencies, stable-context
]))
phases row = [phase-number, phase-id, definition-digest, basis-workset]
prefix row = [phase-number, phase-id, basis-workset,
              checkpoint-result, commit-oid]
```

Phase/prefix lists are in phase order, not hash order. For newly planned
or revalidated pending phases, basis-workset is `"self"`; resolve that
sentinel to the enclosing workset result ID after hashing. It never
serializes the workset's own ID into its payload.

For a completed prefix inherited by a later plan-work revision, keep
the original phase ID and original basis-workset result ID. Verify the
unchanged current `P_n` against that original definition digest, and
resolve the original global digest, dependencies, and stable context
from its preserved plan-work receipt. Do **not** recompute completed
phase IDs against the revised pending work's global frame or context.

The plan-work payload's inherited-prefix is fixed at that work revision's
issuance. It is `[]` initially. Later status changes, checkpoint receipts,
and journal appends do not mutate this payload. Execution derives its
growing completed prefix separately. Only a new plan-work revision can
record a larger inherited prefix. This prevents normal progress from
changing the approved workset.
An initial present-time legacy workset also starts with `"self"` phase
bases and `[]` inherited-prefix: there are no historical v1 identities
to inherit. The recovery gates below must prove its already-recorded
completed prefix before any pending phase executes.

The workset binds the full plan projection, exact upstream results,
stable context, phase mapping, and inherited proof. Changing pending
steps, ownership, verification, global scope, or source/selection makes
a new workset and requires the applicable approval again. A progress
NOTE cannot make that edit authorized. Completed definitions and proven
commits remain immutable; invalidated completed work requires explicit
new undo/redo phases, not rewritten history.
Preserve an In-progress definition too: stop at execution recovery or
the scope-exceeded boundary on drift, rather than calling an active phase
Pending and rewriting it.

Before editing, an implementer reads the plan by path and validates the
supplied workset, phase ID, and literal owned subset. Its executor
validates the same identities again on return. Drift stops the work;
neither role repairs the approval or revises scope by assertion.

## Purpose-Specific Consumption and Recovery

Take these checks at every entry and immediately before acting:

1. Establish the requested purpose and owning artifact. Check required
   installed instructions, parsed controls, pending questions, and known
   domain states. Missing/unknown states never mean success.
2. For active or blocked execution, route dirty phase-owned state or a
   possible unrecorded phase commit to `dev-do` recovery **before**
   declaring upstream results stale. Do not rerun authors to explain
   an interrupted phase's files.
3. Validate receipt chains, recomputed content/result/seal identities,
   dependencies, and applicable approvals. Current and frozen references
   have the different obligations defined above.
4. Compare current context with the stable anchor **plus proven
   progression**, not with a permanently frozen HEAD. Check relevant
   working/index/ignored bytes and the current scope inventory.
5. Apply the owner's domain result. A healthy matching result needs no
   owner redispatch. A missing/stale/insufficient result needs owner-led
   present-time revalidation, or a stop naming the missing evidence.

The domain routing remains explicit: incomplete sources belong to their
source owner; an interrupted/rejected approach belongs to the approach
owner and cannot authorize planning. A Draft plan belongs to planning;
Ready-to-execute requires current work and approval; In-progress or
Blocked belongs to execution recovery. Complete additionally requires
every phase and matching final-verification evidence. If all phases are
complete but the final gate is blocked, missing, or stale, enter
final-only execution recovery rather than redo phases.

### Exact phase-commit progression

`C[7]` is a chronological list of:

```text
[phase-number, phase-id, workset-id, checkpoint-result,
 commit-oid, sole-parent-oid, tree-oid, changed-paths]
```

For a fresh unchanged anchor it is `[]`. Independently prove each row:

- Same repository, worktree, object format, and symbolic branch/detached
  identity as the anchor. A same-SHA branch switch is still a mismatch.
- Every object exists with the required type. Use full OIDs from Git,
  not textual prefix similarity, a ref lookup, or a fixed OID length.
- Each commit has exactly one parent: the anchor commit for the first
  row, then the preceding row's commit. Check the recorded tree and
  exact changed-path set, including both sides of renames as add/delete
  paths with rename detection disabled. Obtain literal names through
  NUL-delimited output or exact Git path-quoting decoding; display quotes,
  shell expansion, and lossy newline splitting are not path identities.
- The COMMIT line, retained pending tuple, phase checkpoint, approved
  workset, and phase definition all identify that same work and object.
  The changed paths are a subset of the exhaustive owned paths.
- Complete every required traversal. The actual anchor-to-observed-HEAD
  history must be exactly these commits, without missing objects,
  unrelated intervening commits, merges, rewritten IDs, or extra
  descendants. Mere reachability is insufficient. Do not consume
  partial command output after a traversal fails. Inspect actual objects,
  not replacement/graft-transformed parentage. Any shallow boundary or
  unavailable history that prevents a required proof refuses; publication
  retains its stronger complete non-shallow history prerequisite.

Validate checkpoint payloads against raw objects before using them in a
progression. Then validate the progression as a separate relation over
those checked tuples; do not recursively presume a suffix valid because
a checkpoint points to it. A checkpoint can name its own result in its
non-operative progression after that result is computed, but cannot
depend on its own seal. A later checkpoint revision must preserve the
phase/workset/commit tuple, never remap completed work.

Every row must belong to the invoking plan's proven chronological phase
prefix, even when its workset was superseded by a pending-work revision.
For upstream source/selection reuse, use that same consumer plan as the
progression witness, not any other plan that happens to cite the result.

The last proven commit/tree is the **effective production anchor** for
comparison now. Do not rewrite the original receipt's stable fields or
force a healthy owner to issue another receipt whenever HEAD advances.
No progression can advance beyond the final proven row.

For a phase-evolving input, derive the expected HEAD/index view from the
effective tree. Use the last checkpoint that changed the path to obtain
its sealed post-commit working-byte view, and verify that checkpoint's
HEAD/index views against the actual commit tree. At capture, establish
that the working view was the clean checkout representation under the
recorded attributes/filter inputs; never assume blob bytes and working
bytes are identical. Compare current raw working bytes with that view,
and recheck the relevant attributes/filter inputs. Missing checkout
evidence requires owner revalidation, not adoption of today's bytes.
Inputs outside the proven changed paths keep their captured views.
Unexplained index, working-copy, untracked, deleted, or consulted ignored
changes still mismatch.

Stable source/selection/work inputs must still match their projections
and dependencies. A phase's commit cannot authorize editing them.
Changed loaded instructions require the reload boundary even when the
commit is otherwise an exact suffix. Stop after the executor's permitted
durable checkpoint; a changed conductor requires a fresh conductor
invocation, not merely a new child or an in-session reread.

### Interrupted execution and legacy evidence

**At every execution/recovery entry, require the clean-index gate before
any executor plan/evidence write.** A nonempty index refuses without
editing the plan, staging, unstaging, resetting, or stashing. The later
authorized staging/PENDING write inside the phase protocol is unchanged;
staged residue on a new invocation is not that authorization.
Keep every existing phase gate: before a Pending phase, exhaustive
literal owned paths must be clean and committable (tracked or nonignored);
ignored control/scratch paths cannot be owned or force-added. Recheck the
index after implementation, capture the exact staged tree/path set,
path-limit the commit, and verify its actual parent/tree/paths before
recording completion. Receipts replace none of these gates.

For an active phase, inspect its owned paths and PENDING tuple. A
possible unrecorded commit must first pass the existing independently
verified sole-parent, staged-tree, and exact-path proof. Only then may
the executor replace PENDING with COMMIT and seal its checkpoint.
Similar filenames, an empty working tree, a REVIEW marker, or a
reachable commit is not recovery evidence. With no matching commit,
resume from the last proven gate and repeat verification as needed.

Do not use a v1 receipt to weaken legacy commit proof. A legacy PENDING
can be reconciled only under the existing tuple protocol; any new receipt
records present-time verification of that proof, not an invented v1
observation before the old commit.

For missing/stale receipts, revalidate at the owning stage against
current inputs and independently proven history. Label the basis
`present-time-revalidation` and retain its limits. Preserve completed
phases and real COMMIT/NOTE history. Revalidate pending work, obtain
required work-bound approval, or plan explicit undo/redo work. Unprovable
execution or required approval stops at its owner; current bytes cannot
reconstruct what was historically read or approved.

**Bootstrap legacy identities without a dependency cycle.** A plan with
completed legacy phases has neither an old workset ID nor checkpoints.
Do not require each nonexistent receipt to authorize creation of the
other, and do not claim new IDs existed before activation:

1. Revalidate source/selection at their owners. The plan owner captures
   the current definitions and real legacy journal, inspects the recorded
   commits, and issues an initial present-time plan-work receipt with
   `"self"` phase bases and no inherited-prefix. Record
   `legacy-inputs-unproven` and `legacy-prefix-unreconciled` in limits.
   Preserve completed/active statuses; this receipt supports executor
   reconciliation only, not pending implementation or a Complete claim.
   Any required approval of pending work binds this new workset.
2. With a clean index, the executor reconciles **every** recorded
   completed phase in order. Require a durable pre-existing COMMIT
   record, a resolving actual commit, sole-parent order, complete
   traversals, tree identity, and exact changed paths within that
   preserved phase's owned set. Inspect its actual diff against the
   retained definition. Establish the required historical approval and
   verification from independently available evidence, or stop. A
   present-time confirmation cannot retroactively supply historical
   interactive consent.
3. For such an already-recorded legacy commit only, a checkpoint may
   use `"legacy-recorded"` instead of an `e-sha256:` pending-evidence ID.
   Use present-time-revalidation basis, prior-result `"legacy-unsealed"`,
   and limits `legacy-inputs-unproven` and
   `legacy-precommit-observation-unavailable`. Its base/tree/paths are
   the actual objects inspected **now**, not an assertion about a lost
   staging observation. Its `C[6]` and observation boundaries record
   today's context, not historical working bytes. Retain check
   evidence's real provenance; if only new verification can establish
   a required property, perform it through the sanctioned owner gate
   on the revision that property requires, or stop as unavailable.
4. After the prefix is proven, the plan owner may append a new seal of
   the **same** workset removing `legacy-prefix-unreconciled`, provided
   all operative bytes/dependencies/stable context still match. Keep
   `legacy-inputs-unproven` and the truthful revalidation basis. The
   executor still derives and checks the prefix from checkpoints.
   Do not revise the payload merely to insert newly obtained proof.
   If reaching that owner requires a backward hand-off, return the
   evidence requirement; this procedure does not waive the fixed-chain
   or owner-write boundary.

This limited reconciliation use of a provisional workset does not waive
any write, index, history, or approval gate; no other consumer treats its
unresolved-prefix limit as reusable. A legacy-recorded checkpoint can
prove an existing historical COMMIT and prefix, **not** an unrecorded
commit or an old clean checkout representation. Possible unrecorded
commits still require the original PENDING parent/tree/path tuple.
If later suffix comparison requires unavailable old working-byte
evidence, revalidate at the current anchor instead. Do not manufacture
a typed pre-commit record after the fact.

At initial resume, route to the earliest owner needing reconciliation.
A later-stage discovery requiring an earlier read-only artifact hands
back naming that artifact/owner instead of silently extending the chain
backward. Retain unchanged-output dispatch diagnostics and bounded
retries: they detect a different failure from stale-input evidence.
An honest rejection is not retried until it becomes a winner.

## Shared Progress Log Decoder

**Decode once; keep commit records separate from review checkpoints.**
In the single root-level `## Progress Log`, allow only blank lines and
these complete single-line records. A wrapped physical line, fence,
quotation, unknown label, repeated key, or malformed recognized record
refuses the journal; never silently skip it or fall back to another scope.

```text
- PENDING | phase: <n> | base: <oid> | tree: <oid> | paths: <paths>
- COMMIT | phase: <n> | sha: <oid> | subject: <subject>
- NOTE | phase: <n> | <text>
- REVIEW | iteration: <k> | planning-complete | receipt: <seal-id>
- REVIEW | iteration: <k> | complete
```

Keys and spacing are exact, with no trailing whitespace. `n` and `k`
are positive decimal integers. Subject/text is a nonempty remainder of
the line; embedded pipes in that remainder are text, not additional
keys. PENDING paths is a nonempty comma-separated list of literal
repo-relative paths, no padding around separators. Sort the exact set
by path, with no duplicates. A comma, leading/trailing whitespace, or
unrepresentable name in a path cannot be encoded by this legacy form;
stop before execution rather than split or trim it into another path.

New writers emit full Git OIDs. Readers accept a legacy hexadecimal
prefix only when Git supports it and `git rev-parse
--disambiguate=<hex>` returns exactly one object. Require actual commit
type for every COMMIT via object inspection; never resolve a hex-looking
ref, revision expression, range, tag, or peeling/ancestry suffix.
Validate PENDING's base/tree types at execution recovery. Decode errors
are not an empty commit set. Preserve raw records alongside normalized
full identities for diagnostics.

Return separate ordered lists of pending records, commits, notes,
receipt-backed review markers, and legacy review markers. Execution
additionally rejects duplicate phase commits or incompatible pending
claims. A commit-only reader can deduplicate multiple valid references
to the same commit as its existing scope contract permits, but must
validate **every** collected token first.

For review, validate that set against the requested concrete scope and
captured history; plan-slot identities must be reachable from its
captured head. An explicit historical scope may intentionally name a
different target, but never fall back to it after stale plan-slot proof.
Collapse a commit set to a range only after proving contiguity (each
commit's sole parent is the preceding commit); otherwise inspect exactly
the individual commits and union their scope. A valid object outside
the requested scope is still a refusal, not a token to drop.

Review/publication commit extraction ignores syntactically valid
PENDING, NOTE, and both REVIEW forms as non-commit records. It does
not derive approval or publication authority from a receipt. Review
progress consumption additionally resolves each new REVIEW seal to the
corresponding `review-plan` chain in this plan. A missing, stale, or
wrong-kind seal cannot prove planning. A syntactically valid marker
does not force a commit-only publisher to adopt review-progress policy.

Keep the journal append-only. PENDING-to-COMMIT, after exact identity
checks, is the **only replacement**. Preserve every COMMIT, NOTE, and
old REVIEW line. Initial planning seeds the journal empty; `dev-do`
writes phase records, and only the remediation `dev-plan` pass writes
receipt-backed REVIEW markers. Write its receipt and marker in the same
owner update, read back both, and recheck before reporting completion.
An interrupted half-update authorizes no planning completion.

### Planning completion versus iteration completion

The legacy `complete` REVIEW form is historical, non-commit evidence.
It proves neither settled planning nor executed fixes. The plan owner
may reconcile it and append a receipt-backed marker; never rewrite the
legacy line into a new claim.

For one iteration, multiple new markers are valid only when their seals
are successive revisions in that iteration's single review-plan chain.
Require a marker naming the active tip, reject duplicate markers for
the same seal or forks, and keep superseded markers as history. An older
marker cannot stand in for a missing active marker.

Compute progress through a **contiguous prefix starting at iteration 1**:

1. A completed review with unsettled dispositions, questions, or required
   approval returns to its recorded planning step. It has no usable
   planning-complete marker.
2. A current planning-complete marker with pending remediation phases
   or outstanding final verification goes to `dev-do`, even though the
   marker exists. It cannot hide pending work.
3. The iteration is complete only after planning is settled and every
   mapped remediation phase and final gate is proven for the required
   workset. For no remediation, require the already-completed execution
   basis the review consumed; invent no extra phase or commit.

Stop advancing at the first gap or incomplete iteration, never at the
highest marker number. Preserve the configured budget: planning retries
and owner revalidation do not grant an extra review pass. One iteration
can leave a review of the pre-fix tree plus executed but unreviewed fixes.
Only another configured iteration or a separate request reviews those
fixes. Report the frozen reviewed revision, executed revisions, surviving
analysis iteration, and fixes not subsequently reviewed.

For publication, retain all existing object, reachability, complete
history, inventory-equality, private-receipt, and approval gates in
`dev-pr-open` [Slot equality](../../dev-pr-open/SKILL.md#slot-equality).
This decoder changes recognized records, not the publication algorithm.

## Computed Byte and Identity Examples

These are **calculation specimens, not owner-produced authorizations**.
They do not claim a review, user approval, or a live Git context. The
manual matrix below still requires owner-produced throwaway fixtures.
The escaped strings describe input bytes, not how an artifact should be
authored. Decode JSON strings once and encode them as UTF-8.

### Byte projection

Use this exact input, including its BOM, CRLF, and non-ASCII character:

```json
"\ufeff# Example\r\n| | |\r\n|-|-|\r\n| Status | Ready-for-plan |\r\n| Created | 2026-01-02 |\r\n| Issue | not published |\r\n## Notes\r\nKeep café.\r\n## Handoff Evidence\r\n"
```

Its source projection excludes only the three validated metadata rows:

```json
"\ufeff# Example\r\n| | |\r\n|-|-|\r\n## Notes\r\nKeep café.\r\n## Handoff Evidence\r\n"
```

These values were calculated twice from those same bytes. The progress
variant changes `Ready-for-plan` to `Refining` and `2026-01-02` to
`2026-01-03`; its row syntax stays valid, but it is no longer ready:

| Specimen | Projected bytes | SHA-256 of projection |
|-|-|-|
| Original | 72 | `598913eb4c0200a6c6ef77af9868187ebbf53790bb1565b83f22fecae09ed5b0` |
| Status/Created syntax only | 72 | `598913eb4c0200a6c6ef77af9868187ebbf53790bb1565b83f22fecae09ed5b0` |
| Operative Notes change, `café` to `cafe` | 71 | `5c25d1734427a34c208542062b2377f94eaa985c34471510451a9915d6cc280c` |
| Same original text with LF rather than CRLF | 66 | `3d4ce380c4a6b1ce37584f0de4a98e8a24686ad31924954123066ba87cc55815` |
| Original without BOM | 69 | `5e72a33ee196e2e3fef0b6a2c973cebaa39391d1895bfa9ad7f28c91a0669990` |

Changing a valid status can retain this digest while changing authority.
An unknown status, duplicate Status row, truncated receipt, or unsupported
handoff version produces **REFUSE**, not another projected digest.
Putting a fake Selected heading inside a quote/fence cannot create a
current selection, and those example bytes still change the projection.

### Ordered serialization and approval separation

```text
S(["é",[],"A"]) = l3:s2:él0:s1:A
```

Its UTF-8 byte count is **15**, its hex bytes are
`6c333a73323ac3a96c303a73313a41`, and its SHA-256 is
`ded334e7c39358f52773c4389f09be4f3ab600ea25335b5d843bab5a312b567b`.
The length prefix for `é` is 2, not 1. Reordering a dependency/manifest
set is rejected at parsing, not accepted as a different incidental order.
Changing a scalar's case or Unicode representation changes its bytes.

Use the following complete recipe to reproduce the envelope vector.
Aliases in this recipe denote values, not strings containing the alias.
`repeat` makes the specified number of identical ASCII characters.

```text
R = D(S(["handoff-v1","repository","fixture-common-dir"]))
W = D(S(["handoff-v1","worktree","fixture-git-dir","fixture-root"]))
L = [R,W,"symbolic","refs/heads/example","sha1"]
A = repeat("1",40)
T = repeat("2",40)
C = [L,[A,T],[],[],[],["none"],[L,A,T,T,[]],[]]
B = ["production","none",[],[],[]]
kind = "source"
subject = "scratch/0101-01/featurerequest.md"
revision = "1"
supersedes = "none"
dependencies = []
content-digest = D(the original 72-byte source projection above)
result-identity = the result formula above, using C and B
approval = ["not-required",result-identity,"standalone","none","none",
            "encoding-only"]
```

Use all envelope rows in their declared order, with that `C` for context
and `B` for evidence-basis. For the second seal, change only approval to:

```text
["pending",result-identity,"interactive",subject,
 D(U("pending specimen\r\n")),"encoding-only"]
```

The computed repository input `R` is
`sha256:faa99277cd9971f22befcfa3c5e5d5c8ec510f9ddebce4ba3194ada73ddd53aa`.
The worktree input `W` is
`sha256:67150c5118d868170081cd1c1a3d741844c69a9117a01f5411d4d527fe36273b`.
The pending record's digest is
`sha256:de38ed692fe66c4fbfa5382ca4dc548d295d4d2479e0b27442e777b14db5047f`.
The two seal streams have **1245** and **1338** bytes, respectively.

These intentionally synthetic location/OID strings, empty observations,
and encoding-only authority code **do not validate as production
evidence**. No Git objects or durable decision records are asserted.
They exercise the hash codec without fabricating user approval.

| Value | Computed identity |
|-|-|
| Source result, both approval variants | `r-sha256:f3bd5b477878395bd706786e094e387e2a78a2f034629fda5a80927e3a2c3e1f` |
| Seal with not-required approval | `s-sha256:2dd253aaca22a593390a19c6b977b9ecfa2681ce3f31e10b67aa314fde987ce4` |
| Seal with pending approval | `s-sha256:b8418b2fb347dc25eda06024c5d798efe10ad327193eb22092c56b227fde0540` |

These are **alternative revision-1 vectors**, not two receipts to append
to one chain. A real changed approval appends revision 2 and supersedes
the first seal, so that revision's seal would differ from both vectors.

Here is the complete table encoding of the first vector. Keep each row
on one physical line. With CRLF on all 16 lines and no extra blank lines,
the block is **1395 bytes**. The fixture codec re-read the table,
verified all three identities, and projected the original 72 bytes when
this block was appended immediately after Handoff Evidence. Full domain
validation still refuses these synthetic observations, as stated above.

```markdown
<!-- handoff-v1:receipt begin -->
| key | value |
|-|-|
| version | "handoff-v1" |
| kind | "source" |
| subject | "scratch/0101-01/featurerequest.md" |
| revision | "1" |
| supersedes | "none" |
| result-identity | "r-sha256:f3bd5b477878395bd706786e094e387e2a78a2f034629fda5a80927e3a2c3e1f" |
| content-digest | "sha256:598913eb4c0200a6c6ef77af9868187ebbf53790bb1565b83f22fecae09ed5b0" |
| dependencies | [] |
| context | [["sha256:faa99277cd9971f22befcfa3c5e5d5c8ec510f9ddebce4ba3194ada73ddd53aa","sha256:67150c5118d868170081cd1c1a3d741844c69a9117a01f5411d4d527fe36273b","symbolic","refs/heads/example","sha1"],["1111111111111111111111111111111111111111","2222222222222222222222222222222222222222"],[],[],[],["none"],[["sha256:faa99277cd9971f22befcfa3c5e5d5c8ec510f9ddebce4ba3194ada73ddd53aa","sha256:67150c5118d868170081cd1c1a3d741844c69a9117a01f5411d4d527fe36273b","symbolic","refs/heads/example","sha1"],"1111111111111111111111111111111111111111","2222222222222222222222222222222222222222","2222222222222222222222222222222222222222",[]],[]] |
| approval | ["not-required","r-sha256:f3bd5b477878395bd706786e094e387e2a78a2f034629fda5a80927e3a2c3e1f","standalone","none","none","encoding-only"] |
| evidence-basis | ["production","none",[],[],[]] |
| seal-identity | "s-sha256:2dd253aaca22a593390a19c6b977b9ecfa2681ce3f31e10b67aa314fde987ce4" |
<!-- handoff-v1:receipt end -->
```

Truncating its end marker, changing its version, duplicating a key, and
corrupting its seal each returned **REFUSE** in that codec inspection.

### Pending revision without rewriting a completed prefix

Construct this exact calculation input by joining the following strings
with CRLF and adding a final CRLF, with no BOM or other blank lines:

```json
[
  "# Plan example",
  "| | |",
  "|-|-|",
  "| Status | Ready-to-execute |",
  "## Phases",
  "Keep the scope fixed.",
  "### Phase 1: One",
  "**Goal:** First.",
  "**Owned paths:**",
  "- `a.md`",
  "**Steps:**",
  "1. Edit a.",
  "**Verification:**",
  "- Inspect a.",
  "**Status:** Pending",
  "### Phase 2: Two",
  "**Goal:** Second.",
  "**Owned paths:**",
  "- `b.md`",
  "**Steps:**",
  "1. Edit b.",
  "**Verification:**",
  "- Inspect b.",
  "**Status:** Pending",
  "## Final Verification",
  "- Inspect both.",
  "## Progress Log",
  "## Handoff Evidence"
]
```

The progress variant changes the top status to In-progress, Phase 1's
status to Complete, and inserts
`- NOTE | phase: 1 | recorded progress` immediately after Progress Log.
This is a syntax/projection example, **not proof of phase completion**.
The work variant instead changes `- Inspect b.` to `- Inspect b twice.`
and otherwise leaves the original input alone.

| Projection | Bytes | SHA-256 |
|-|-|-|
| Plan, original and progress variant | 368 | `5b665e7192dae4dc73d38da5b268e0b9ecefabdf9431f544c543e4a411df2dfb` |
| Plan after Phase 2 verification edit | 374 | `d35c3b3676e750015f894812669b208b80295a0130dbb746c4d434e457c6d4e1` |
| Phase 1 definition, both work variants | 121 | `e0f1620dcb319aad8a74ec176a6226f6b7b770651c3e9d72bfc12ce510e449e5` |
| Phase 2 definition before edit | 122 | `9e3a83f5859242b9fb06ede7be292a84dadff4ce76522b6bdd1142a1f090f655` |
| Phase 2 definition after edit | 128 | `9570cc985d42ef2056c7eded3c3e47c07f7cdcc67a96b793a43c26838fa4c821` |
| Global frame, both variants | 125 | `bef5422b96ba85a96a54ffac97eeb6c62f0fb494b43c5909455f3b50c8f73e75` |

The global frame is exactly:

```json
"# Plan example\r\n| | |\r\n|-|-|\r\n## Phases\r\nKeep the scope fixed.\r\n## Final Verification\r\n- Inspect both.\r\n## Handoff Evidence\r\n"
```

For the identity calculation, use the same synthetic `C` above and plan
path `scratch/0101-01/plan.md`. Initial dependencies, in order, are:

```text
["selection","scratch/0101-01/approach.md","not-used","none","none"]
["source","scratch/0101-01/featurerequest.md","current",
 the source result above,the original source content digest above]
```

Derive both original phase IDs using those dependencies. The original
plan-work payload has the measured global digest, the two measured phase
definitions/IDs with basis-workset `"self"`, and inherited-prefix `[]`.
Call its computed workset `W0`.

To exercise inheritance without claiming a real commit, set synthetic
commit `J = repeat("3",40)` and tree `Q = repeat("4",40)`. Calculate
pending evidence for phase 1 with data
`[W0,phase1-id,A,Q,["a.md"],[]]`. Calculate a checkpoint payload
`[W0,"1",phase1-id,pending-id,A,Q,["a.md"],J,[]]` with `C` and one
frozen plan-work dependency on `W0` and its original content digest.
Use the typed payload projection for its content digest. Empty checks
and unverified objects again make this arithmetic, **not execution
evidence**.

For the new workset, prepend the resulting frozen phase-checkpoint
dependency to the original dependencies. Use the edited Phase 2
definition to derive its new phase ID. Carry Phase 1's original ID with
basis-workset `W0`, and use inherited-prefix
`[["1",phase1-id,W0,checkpoint-result,J]]`. Phase 2's basis is `"self"`.
The new workset uses the edited full plan projection and unchanged
global digest and stable context.

| Identity | Computed value |
|-|-|
| Phase 1 in both worksets | `p-sha256:895bc288d5c5e83a7e3efc299da872a8e9a9cf30e40b77f9988cbaeded7e8f60` |
| Original Phase 2 | `p-sha256:9a12e3f43a948dd3ceab7dbee824ed6d13dda184b7c363168d5012375e45319c` |
| Pending evidence | `e-sha256:e2b28c9c50ddc22f2b6a876fd3efbb225d9d64446a0133f7a1135d4c4f1871d9` |
| Checkpoint content digest | `sha256:20bc147ba7d8aed5e0421a15eb0e70117ca13a0174306285d5ffce4694184a8c` |
| Checkpoint result | `r-sha256:f665734283990a750d24353397aef135da5ad85b7896040d30e29720797e7864` |
| New Phase 2 | `p-sha256:103491d1ead894ece16c0827031254fded260df8f3bf516befc306dfb293bb18` |
| Original/progress workset W0 | `r-sha256:cd03d093598dba580e4308dc0617bc3329d61f11fbd80a0ab4f6b644af4009c1` |
| Revised workset | `r-sha256:73423000afeba3099c90206820f4da1aac56d5da5b3f4b2dace9ebe5f2010f69` |

In real use, independently prove that prefix and approve the new workset
before implementing Phase 2. Never update the old payload as phases
complete. The calculation's unknown-status, duplicate-Status,
truncated-receipt, unknown-version, and malformed-journal perturbations
all returned **REFUSE**. This is not a claim that a live worker or a
complete CommonMark validator was exercised.

## Manual Conformance Matrix

Use fresh owner-produced fixtures outside the repository, perturb copies,
and distinguish calculations/static inspection from executed behavior.
Run applicable cases through standalone and orchestrated consumers.
Do not mutate historical slots or infer approvals for a fixture.

| Case | Stimulus | Required observation |
|-|-|-|
| T1 — optional versus started | No approach artifacts; then a lone author, skeleton, or deleted prior dependency | Only genuinely not-started standalone planning records not-used; started cases refuse at the approach owner |
| T2 — malformed controls | Empty selection, missing/inactive candidate, duplicate current control, quoted Selected, corrupt override | Both consumers apply the same validator; no heading-only success or fallback winner |
| T3 — selection/rejection | Valid selected result and eligible actual user override; reject all candidates in each mode | Honor current approval; preserve reasons and non-selection; never force a winner or waive an invariant |
| T4 — operative changes | Edit ready source, candidate, override, custom Notes, or phase verification without changing Status | Affected result/workset and approval stop authorizing reuse; the appropriate owner reconciles |
| T5 — evidence failure | Missing/truncated receipt, unknown version/status, duplicate tip, fork, cycle, missing predecessor | REFUSE with owner/evidence diagnostic; no fabricated baseline or last-record-wins |
| T6 — same-SHA branch switch | Switch a sealed fixture to another branch at the same commit | Ref identity mismatches even though commit/tree match |
| T7 — context/history | Edit consulted ignored instructions; move/switch worktree; unrelated commit, rewritten commit, unavailable required history | Refuse/reconcile; clean status, reachability, or an incomplete traversal cannot establish freshness |
| T8 — ordinary progress | Successive exact owned phase commits plus valid status, ledger, and journal appends | Work identity is stable; derive the effective anchor without upstream regeneration |
| T9 — interruption | Stop before commit, after commit before COMMIT replacement, and with staged residue | Exact PENDING parent/tree/path proof governs recovery; nonempty index refuses before any plan/evidence write |
| T10 — final-only/decisions | All phases complete but final gate blocked/missing; QUESTION or stale confirmation on ready work | Executor reruns only the final gate; decisions return to their owner and never become consent |
| T11 — snapshot/change | An established old defect in a named-file snapshot and a diff limited to that file | Snapshot may report it; unchanged old defect is out of remit in the diff, not falsely zero-confidence; every reviewer agrees |
| T12 — measurement/drift | Unchanged files on both sides of the file/line threshold; mutate one during review | Snapshots do not measure zero; correct independent dispatch; no mixed-snapshot seal |
| T13 — candidate modes | Collapsed judge-only; contested-to-collapsed with edited inactive files; re-expand | Judge the declared one-or-three active set; preserve inactive bytes/provenance; reconcile before reactivation |
| T14 — planning interruption | Clean and actionable reviews; interrupt before approval and after marker with pending fixes | Clean adds no work/commit; pending approval remains pending; retry adds no duplicate phases; execution still runs |
| T15 — journal mixtures | Both REVIEW forms, valid COMMITs, malformed tokens, wrong-type/stale OIDs, iteration gaps | Invalid commits never disappear into fallback scope; publication gates survive; only contiguous completed iterations advance |
| T16 — budget/reload | One versus two review iterations; change loaded executor, conductor, role, or reference | Report reviewed versus unreviewed fixes; no free pass; require the appropriate fresh invocation |

**Report observational limits.** These checks detect observed drift;
they are not locks, transactions, cryptographic signatures, or protection
against an actor rewriting all evidence. Never report an unavailable
installer check, fresh-session rehearsal, or tool calculation as passed.
