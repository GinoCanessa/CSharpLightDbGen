---
name: dev-pr-open
description: "Publishes committed work on the current branch as a pull request, in the role of a release engineer. USE FOR: explicitly pushing the branch, approving the complete PR title/body, adding one fully approved changelog-only commit when needed, and referencing bound issues. A named slot's plan commits must exactly match the captured branch work; receipt-proven publisher commits remain visible separately. With no slot, branch mode publishes the complete default-base/head range, including unmatched work. Both modes require verified non-shallow history. Accepts a full path to `plan.md` or a numeric slot. Opt-in: requires `AGENTS.md` GitHub Integration with `Enabled: yes`. The only skill permitted to push or open a PR; never force-pushes, rewrites branches, or edits slot artifacts. Changelog, push, and PR-write approvals are separate."
---

# Dev PR Open Skill

Acts as a **release engineer** for the final step of the local inner
loop: taking committed local work and turning it into a pushed branch
and an opened pull request.

It runs in one of two **scope modes**, and every section below is
written against both:

- **Slot mode** — require the slot's recorded commits to equal all work
  in the captured branch range. Account for receipt-proven publisher
  changelog commits separately, without hiding them.
- **Branch mode** — publish every commit in the captured range ahead
  of the default branch, regardless of slot provenance. This is the
  default when the user names no slot.

**Both modes require complete, verified non-shallow history.** Pass the
history gate in *Preconditions* and complete every required history
traversal before trusting a scope. An apparently matching local range
in a shallow checkout is not proof.

**Neither mode publishes an arbitrary subset of a branch.** Capture one
inventory and use it for scope previews, approvals, PR content, and the
report. A named slot that covers only part of the work is a refusal,
not permission to describe that part while pushing the whole branch.

This is the **only** skill permitted to `git push` or to open a pull
request. `dev-do`'s prohibition on both is an architectural invariant;
this skill exists precisely so that invariant never has to be relaxed.

It is **opt-in and off by default**. When the repository's `AGENTS.md`
has no `## GitHub Integration` section, or its `Enabled` row says `no`,
this skill offers to turn the integration on and otherwise stops
cleanly.

## Role

You are a **release engineer**. That means:

- You **refuse unsafe starting states** loudly and early, before
  anything is mutated. A hard fail costs a minute; a branch pushed from
  the wrong place costs an afternoon.
- You **name the scope before you act on it.** Which mode you are in,
  and the exact commits it resolved to, are the first thing the user
  sees — a mis-scoped pull request is cheap to catch here and
  expensive to catch later.
- You **approve the whole change**. Show the complete changelog patch
  and message before committing, and the complete scope and PR content
  before pushing. Obtain separate approval for the PR write.
- You **make re-runs evidence-backed**. Use committed anchors and
  verified receipts, not plausible subjects or dirty files. Refuse
  uncertain recovery instead of adding a duplicate.
- You **report exactly what happened** — which commits, which branch,
  which URL.

## Inputs

1. **Scope** *(optional)* — what to publish. One of:

   **Slot mode**, selected by naming a slot:
   - A **full path** (absolute or repo-relative) to a slot's
     `plan.md`. Used verbatim; the slot is that file's directory.
   - A **slot number** (one or more digits, e.g. `2`, `02`, `14`).
     Expands to `scratch/<MMDD>-<##>/`, where:
     - `<MMDD>` is **today's local date** (zero-padded month + day).
     - `<##>` is the slot number, **always zero-padded to two digits**.
   - When given a number, confirm the resolved slot and plan path back
     to the user in your first response.
   - If the resolved `plan.md` does not exist, stop and tell the user.

   **Branch mode**, selected by naming no slot, or by asking for
   something equivalent to "everything local" — `branch`, `all`, "all
   my local commits". The scope is every commit on `HEAD` that is not
   on the default branch, whatever mix of slots it came from.

   **When the user names nothing, branch mode is the default.** Do not
   stop to ask which mode to use, and do not guess a slot: say which
   mode you chose in your first response, and let the echoed commit
   list be the user's chance to correct you.

2. **Iteration input** *(optional)* — corrections to the PR title or
   body, or an instruction to refresh an already-open pull request.

## Read the Shared Protocol First

Before resolving **any** configurable value, open
`.github/skills/dev-issue/SKILL.md`, read its
`## Resolve-and-Record Protocol` section, and follow it **verbatim**.
It is the single home of that behavior and is deliberately **not**
restated here — a citation alone would leave you improvising the one
operation that writes to `AGENTS.md`.

**If that file is absent, stop.** Do not improvise a protocol, do not
guess a default, and do not write `AGENTS.md`.

## Preconditions

Run this gate in exactly this order, before changelog edits, staging,
commits, receipt writes, pushes, or GitHub writes. Read-only range
capture below supplies step 7; defer pending-receipt completion until
these gates pass. Configuration recording remains confined to the
shared protocol.

1. **Integration enabled.** Read `AGENTS.md` at the repository root and
   locate the `## GitHub Integration` section. Proceed only when its
   `Enabled` row says `yes`. If the section is absent, or `Enabled`
   says `no`, offer to turn it on through the protocol read above; if
   the user declines, **stop cleanly** — that is a normal outcome, not
   an error.

2. **`gh` is present and authenticated.** `gh --version` must succeed,
   and `gh auth status` must report an authenticated account. On
   failure, stop and report the exact error verbatim.

3. **Remote cross-check.** Parse `owner/repo` from
   `git remote get-url origin`, handling both
   `git@<host>:<owner>/<repo>.git` and
   `https://<host>/<owner>/<repo>` with an optional `.git` suffix.
   Compare it to the recorded `Repository` row. On **any** mismatch,
   **stop and ask**. A fork inherits the upstream's tracked
   `AGENTS.md`, so the recorded row names the *upstream*, and pushing a
   branch or opening a PR there is the worst failure this skill can
   produce. This is the same check `dev-issue` performs.

4. **Clean index.** `git diff --cached --quiet` must exit 0. A
   non-empty index is a **hard stop**: leave it exactly as found and
   report it. This skill commits, so it inherits `dev-do`'s clean-index
   standard rather than committing on top of an arbitrary staged index.

5. **Hard fail — detached or default-branch `HEAD`.** Require
   `git symbolic-ref --quiet HEAD` to return `refs/heads/<branch>`.
   Capture that full ref and strip only `refs/heads/` for the branch
   name. A detached `HEAD` stops; never create or switch branches to
   repair it. Resolve the default branch two ways and require them
   to agree:

   ```powershell
   git symbolic-ref --quiet --short refs/remotes/origin/HEAD
   ```

   (strip the leading `origin/`), and, on failure:

   ```powershell
   gh repo view <owner/repo> --json defaultBranchRef `
     -q .defaultBranchRef.name
   ```

   If the two disagree, or neither resolves, **stop and ask**. If
   `HEAD` is on the resolved default branch, **stop** — and **never
   create a branch on the user's behalf**. Branch creation is the
   user's decision. Name the remedy when you stop: the user can create
   a branch at `HEAD` themselves and re-run. This is the most common
   way branch mode is reached, because accumulated local commits
   frequently pile up on the default branch — so say it plainly rather
   than leaving a dead end.

6. **Hard fail — incomplete or unverified history.** Run:

   ```powershell
   git rev-parse --is-shallow-repository
   ```

   Require exit 0 and the single boolean value `false`. A `true`
   result, a nonzero exit even with apparent `false` output, empty or
   unexpected output, or an unavailable check is a hard stop. Report
   the observed result, exit status, and error; unknown is not proof
   of non-shallow history.

   This gate is unconditional in both modes, even with `Changelog file`
   set to `none`, committed anchors covering every candidate, existing
   publisher receipts, or an already-remote head whose push would be a
   no-op. Refuse every shallow repository, even if its boundary seems
   unrelated to the intended range. Never deepen or unshallow history,
   manipulate shallow metadata, rewrite branches, switch modes, or
   ask for approval to bypass missing proof. Require the operator to
   supply a complete-history checkout and re-invoke independently.

7. **Hard fail — no commits in scope.** If captured `R` below is empty,
   there is nothing to open a pull request for. Stop and say so. An empty
   slot record set also stops under the separate slot-equality gate;
   it never selects branch mode.

8. **Warn and ask — uncommitted work in scope.** Apply the existing
   dirtiness domain for the selected mode:
   - **Slot mode** — the plan's owned paths, reusing `dev-do`'s
     standard: every literal owned path of every phase, tracked and
     untracked, staged and unstaged.
   - **Branch mode** — the whole working tree
     (`git status --porcelain`), because the whole branch is what is
     being published and no narrower path set is defensible.

   Modified **tracked** paths in that domain always trigger the ask.
   Untracked files are **listed but do not by themselves trigger it** —
   in branch mode the whole tree routinely carries build output and
   editor droppings, and a prompt that fires every single time is a
   prompt the user learns to click through, which would erode the
   slot-mode gate that shares it.

   When it triggers, show what is dirty and ask whether to proceed
   anyway. Proceeding is the user's explicit call, not your default.
   This warning never overrides the clean-index or hard changelog-target
   gates below.

## Commit Scope Resolution

Reuse `dev-review`'s `COMMIT`-entry parsing, then apply the publication
identity and set checks below. A plan is evidence of recorded work, not
authority to publish a different branch range.

### Capture the range and existing PR

After the integration, authentication, remote, index, branch, and
history gates, verify the repository's immutable ID and `full_name` with:

```powershell
gh api repos/<owner>/<repo>
```

Require the returned name to agree with the cross-checked `Repository`
row. Capture that ID/name, the local repository root, origin target,
and current branch. Refresh the default-branch ref explicitly, because
an opportunistic fetch may not create a ref a single-branch clone lacks:

```powershell
git fetch origin `
  +refs/heads/<default-branch>:refs/remotes/origin/<default-branch>
```

If fetching fails, stop and report it. It updates remote-tracking refs,
not tracked work. Repeat the complete-history gate from *Preconditions*
after this fetch, before deriving or trusting the inventory. Capture
the default base name and its fetched full OID, and the full `HEAD` OID.
Use Git's repository object format, never an assumed OID length.

Fetching only the base and resolving both endpoint OIDs do not prove
complete feature ancestry. Require every necessary history traversal to
complete successfully. A failed or incomplete traversal stops with its
diagnostic; partial output is neither an inventory nor proof that no
commits are in scope.

**Validate an existing PR now, before changelog work or push.** List all
pages of open PRs and find candidates for the captured head branch:

```powershell
gh api "repos/<owner>/<repo>/pulls?state=open" --paginate
```

For a candidate, inspect its immutable `id`, number/URL, `base.repo`
and `head.repo` IDs/names, `base.ref`, `head.ref`, and base/head OIDs.
Require exactly one candidate or confirmed absence. A sole candidate's
base and head repositories must both be the verified repository, its
base branch must be the resolved default, and its head branch must be
the captured current branch. Require its base OID to match the fetched
base. Ambiguity, a different base/head identity, or an unverifiable
repository stops; never retarget a PR.

Save that PR identity and its remote head OID, or save absence. Before
push, an existing PR's remote head may differ from the local head;
record it for drift checks, not as range authority. Revalidation must
both rediscover candidates and re-read the recorded PR directly, so a
closed, missing, or retargeted PR cannot quietly select another operation.

### One publication inventory

Require the complete-history gate and complete, successful required
history traversals before defining or relying on these sets in either
mode:

- **`R`** is every commit in captured `base-OID..head-OID`, including
  merges and publisher commits. Do not apply first-parent, no-merges,
  subject, or path filters.
- **`L`** is only the commits in `R` proven by *Private Changelog
  Receipts*. Validate receipts even when no new entry will be written.
- **`W = R - L`** is the work set used for slot attribution and new
  changelog candidates, never a replacement publication range.

This **two-dot commit inventory** is not the **three-dot PR file diff**,
`base-OID...head-OID`, which compares the merge base with the head.
Neither is `@{u}..HEAD`: a push can empty that range, and an upstream
need not exist. **Never remove `L` from what is pushed, echoed,
described in the PR, or reported.**

### Slot equality

In slot mode, first require the complete-history gate and successful
required traversals above; then apply every check before editing:

1. Read the named plan's `## Progress Log` and parse **every `COMMIT`
   entry**. Ignore only `PENDING` and `NOTE`; neither authorizes work.
   A malformed `COMMIT` or missing identity is an error, not an entry
   to skip.
2. Require each identity to be a literal hexadecimal object name, not
   a ref, revision expression, range, or a token with peeling/ancestry
   suffixes. Use `git rev-parse --disambiguate=<hex>` to require exactly
   one object, then `git cat-file -t <full-OID>` to require type
   **commit**. An unsupported short prefix also stops. Do not resolve
   a hex-looking ref or peel a tag to make it pass. Normalize to the
   full OID with Git; textual prefix similarity never grants authority.
3. Require **every** resolved OID to be reachable from captured head
   and present in `R`. Missing objects, ambiguous abbreviations, wrong
   object types, stale rebase IDs, branch-switch leftovers, unreachable
   commits, and commits already on base or otherwise outside `R` stop.
4. Form the distinct set **`S`**. Require it to be nonempty,
   **`S = W`**, and **`S` disjoint from `L`**. Multiple valid
   abbreviations of the same commit normalize to one identity, not
   multiple work items.
5. On failure, report **both** missing work (`W - S`) and extra plan
   identities (`S - W`), including empty sets, plus any overlap with
   `L`. List invalid tokens and their reasons separately; never drop
   them to manufacture equality. Show full OIDs and subjects where
   objects resolve.

Collect all record failures for that report. Diagnostic `S` includes
every resolved commit OID, even an out-of-range one; unresolved or
wrong-type tokens remain explicit errors, never authorization.

A branch containing A+B cannot publish as A. An empty plan cannot switch
modes automatically. Require a separate explicit branch-mode selection
and new review of all work, or a matching branch the user prepares.
Never create, switch, cherry-pick, rebase, or rewrite a branch or plan to
make the equality pass.

**Branch mode takes all of `R` as authority** only after the same
history prerequisite, with best-effort discovery below; it does not
need a slot to authorize any commit. Switching modes cannot bypass the
history gate.

**Echo the same complete inventory before changelog work.** Show the
repository, mode, current branch, base name/OID, head OID, and existing
PR identity or absence. List all of `R` by full OID and subject in
chronological order, annotating work groups and the separate
receipt-proven publisher commits. Include unmatched and trailer-only
work. Groups explain this list; they never filter it.

## Slot Discovery (branch mode)

Branch mode has no single plan handed to it, so it finds the slots that
produced its commits rather than assuming there is one. Use only the
inventory authorized by the complete-history gate and successful
required traversals. Once the complete commit list is resolved:

1. Find candidate slots cheaply: search `scratch/*/plan.md` for work
   SHA prefixes and open only the files that hit. Prefix search locates
   candidates; it does not validate them.
2. Parse each candidate's `COMMIT` entries and normalize identities
   using *Slot equality*'s object and reachability checks. Attribute
   only valid full OIDs in `W`. Report rejected, invalid, stale, and
   out-of-range associations without dropping the actual commits.
   A slot is **in scope** when at least one valid identity matches.
   Label a match **partial** when only part of its recorded work is
   validated in `W`; invalid records also prevent a whole-plan claim.
   Describe only matched work, never imply the entire plan shipped.
3. Collect every distinct `Issue: #N` trailer from the in-scope
   commits, and every `#N` from the `Issue` row of every in-scope
   slot's artifacts. Their **union** is the set of issues this pull
   request closes. The protected-file read ban applies here too:
   never open `review.md` or `approach*.md` for binding discovery.
4. **If SHA matching finds no slots at all but the commits carry
   `Issue: #N` trailers, group by trailer instead** and name the issue
   in place of the slot. A rebase before opening a pull request is
   routine and invalidates every recorded SHA, but trailers survive it
   — so useful grouping survives. Label it **trailer-only attribution**,
   not validated slot provenance. Keep `L` separate from work groups.
5. Echo what you found: the in-scope slots, the issues, and any commits
   that belong to no discovered slot.

**Discovery is best-effort and never a gate.** Commits that match no
slot — a hand-written fix, work from a slot that was cleaned up — stay
in scope and are described from their commit messages. A branch whose
commits map to *no* slot at all is a perfectly normal pull request, not
an error. Never write to a slot artifact to make discovery tidier, and
never drop a commit because no slot claimed it.

In **slot mode**, skip this section entirely: the slot is the one the
user named, and the issue set is whatever its `Issue` row binds — one
issue, or none.

## Private Changelog Receipts

Resolve this publisher's separate private runtime directory with Git:

```powershell
git rev-parse --path-format=absolute --git-path devskills/dev-pr-open/
```

Never hardcode a `.git` directory or write another publisher's receipts.
These are additional runtime writes in Git metadata, not tracked files,
configuration, or slot artifacts. Read existing evidence before
classifying `L`, even when today's changelog setting is `none` or all
anchors are already committed. Create the directory only when storing
an approved pending record.

Use **`changelog-<parent-OID>-<tree-OID>.json`**, with this version-1
pending shape. The repository ID `1` is illustrative; record GitHub's
actual immutable positive integer ID and verified name.

```json
{
  "schemaVersion": 1,
  "state": "pending",
  "repository": {
    "id": 1,
    "nameWithOwner": "<owner/repo>"
  },
  "branch": "<captured-current-branch>",
  "parentOid": "<full-parent-commit-OID>",
  "treeOid": "<full-approved-tree-OID>",
  "paths": ["<exact-repository-relative-file>"],
  "changelogTarget": {
    "kind": "file",
    "path": "<resolved-repository-relative-target>"
  },
  "workOids": ["<full-covered-work-commit-OID>"],
  "anchors": ["<exact-stable-anchor>"],
  "message": "docs(changelog): <subject>\n\n<approved-trailers>\n"
}
```

For fragments, `changelogTarget.kind` is `directory`; `paths` still
lists exact files. Completion changes `state` to `complete` and adds
`commitOid` with the full verified commit OID in **that same record**.
All approved fields stay unchanged. A pending record has no `commitOid`.

**Validate the complete record, not its filename.** Require the shown
types, version, state, and fields, no duplicate JSON keys, and full Git
OIDs of the right object types. Require distinct, nonempty `paths`,
`workOids`, and `anchors`. Paths and the saved target are literal
repository-relative paths, never globs, escapes, or a directory-wide
commit pathspec. Each path must belong to the saved file or fragment
directory. Covered work OIDs must be commits reachable from the approved
parent; anchors must occur in the saved tree's target blobs: the approved
index tree while pending, and the verified commit tree once complete.
Use the saved target and evidence, not today's changelog configuration.
Require repository and branch identity to match the captured operation
before using a record as proof; leave unrelated records untouched.

**Compare the complete intended Git message exactly.** Save and compare
decoded strings with case-sensitive ordinal equality, including all
trailers, whitespace, line endings, and the final newline. Read the
message from the commit object, not a subject or a display formatter
that may add a newline. Never trim, normalize, or accept a plausible
`docs(changelog):` subject as ownership.

**Write atomically through a temporary sibling.** Serialize UTF-8 JSON,
read it back, and validate the complete record before installing it.
Recheck that the destination is still the validated prior record, or
absent for a new pending record. Never truncate or overwrite conflicting
evidence. A failed write or atomic replacement preserves prior evidence
and stops before commit or push; an incomplete temporary file is not
proof. Persist `pending` only after complete patch/message approval and
the immediate pre-commit checks, never as a speculative draft.

**Recognize or recover only exact commits.**

1. A complete receipt can place its `commitOid` in `L` only when that
   commit is in `R` and its **sole parent, tree, exact changed-path set,
   complete message, and repository** match the receipt. Verify paths
   against the parent without rename collapsing or a path filter.
2. For an interrupted pending receipt for this repository/branch,
   inspect all candidate commits in captured `R`. Complete it only if
   **exactly one** matches those same identities and saved evidence.
   After all preconditions pass, repeat the complete-history gate
   immediately before the recovery write. Failure invalidates
   authorization and stops without advancing the receipt, even if all
   captured OIDs are unchanged. Only on success, atomically complete the
   existing record with that OID, then classify it in `L`. Zero or
   multiple matches stops with evidence retained; never choose the
   newest or create a replacement commit. Pending recovery is required
   even with no new entry to write.
3. Missing evidence grants no exception: an unverified changelog-looking
   commit remains visible work in `W`, so it cannot silently pass slot
   equality. Report invalid, unsupported, corrupt, identity-mismatched,
   or conflicting evidence; it grants no `L` status. Stop on invalid or
   conflicting relevant records rather than replacing them. Never
   reconstruct a receipt from current content, anchors, or a subject.

Receipts do not travel with a clone or push. Another machine or worktree,
lost metadata, or a crash can remove bookkeeping proof. Approval cannot
waive missing evidence. Report the receipt path and any unresolved state.

## Snapshot Revalidation

At each boundary named below, compare against the captured snapshot;
**do not refresh the approved inventory to absorb a change**:

- Recheck the local repository root, origin target, configured repository
  cross-check, and verified GitHub repository ID/name.
- Require the same full checked-out branch ref and full head OID.
  Only the verified changelog commit below may advance that head.
- Fetch the same resolved default ref with the explicit fetch above.
  Require its full OID to equal captured base; a failed fetch stops.
- Repeat the complete-history gate from *Preconditions* after that
  fetch. Shallow or unverifiable history invalidates authorization even
  when repository, branch, base, and head identities are unchanged.
- Rediscover all open PR candidates and re-read any recorded PR. Require
  the same identity or absence, base/head repository and branch
  identities, and base OID. Before push, require the saved remote PR
  head unchanged; after the owned push, require the approved head OID
  instead. A PR appearing, disappearing, closing, retargeting, or
  becoming ambiguous invalidates the selected create/update operation.
- Recheck the exact approved content at the approval boundary in
  question. Never substitute a fresh rendering after approval.

Any failed read, incomplete required history traversal, or observed
drift stops before the next side effect and invalidates authorization.
Report the differing fields or history result/error and effects already
made; never retarget, widen scope, recapture a smaller range, or silently
re-approve. Do not claim rollback. These checks detect observed drift,
not an atomic Git/GitHub transaction. The final read/write race remains,
and even matching read-back cannot prove no intervening edit was lost.

## Changelog

Resolve the `Changelog file` and `Changelog entry format` rows through
the protocol read above. **Detection candidates to propose**, in this
order, are the conventional locations:

- `CHANGELOG.md`
- `CHANGES.md`
- `docs/CHANGELOG.md`
- a `.changeset/` directory
- a `changelog.d/` directory

None of these is a value. Each is a candidate the protocol proposes,
confirms, and records; a repository that keeps its changelog elsewhere
answers with its own path. A recorded value of `none` is final and never
re-asked. It means **no new changelog commit**, not permission to skip
the history gate or receipt validation, or to hide existing `L`.

### Candidates and committed anchors

1. **Draft candidates from `W` only.** Slot mode has one change.
   Branch mode has one per in-scope slot, plus one per coherent group
   of slotless commits. Describe partial matches only to the extent
   validated; label trailer-only attribution. Several issues must not
   be buried in one entry.
2. **Give each candidate a stable anchor.** Use the slot id for a slot
   candidate, or the covered commits' short SHAs for a slotless one.
   Retain the chosen anchors. Dedupe on anchors, never issue numbers
   or drafted subjects: two slots may share an issue, and subject text
   can change between runs. Short anchors are not commit-identity proof.
3. **Inspect anchors only in committed content at captured head.**
   Read the configured file's blob, or enumerate and read the directory's
   files from that Git tree. An absent committed target has no anchors.
   Do not follow symlink content. Dirty, staged, or untracked text cannot
   suppress a candidate. Drop only candidates with committed anchors.
4. If every anchor is already committed, **make no new commit**.
   Continue with the same complete `R`, disclosing existing `L`.
   Adding another slot drafts only its missing entry, not a duplicate
   for the earlier slot.

### Exact targets and clean starting state

For remaining entries, draft in memory and enumerate targets **before
editing**: the one configured file, or the exact proposed new fragment
filenames. Freeze this literal repository-relative file set. Never stage
or commit a directory, glob, or every sibling in it.

- **Require safe, regular, committable targets.** Check both lexical
  and resolved containment within the repository. Reject a symlink,
  junction, or other reparse point in the target **or any existing
  ancestor**, including the ancestor chain of an absent new file.
  Reject non-regular files, directories as file targets, Git symlink
  or gitlink modes, ignored paths, and escapes. Inspect actual paths,
  not just string prefixes; do not force-add an ignored target.
- **Require tracked targets to match HEAD in index and worktree.**
  Check contents and modes, not merely a status flag that could hide
  changes. Staged changes, unstaged edits, or a missing tracked file
  stop before editing. Ordinary dirtiness approval cannot waive this.
- **Require new targets to be genuinely absent.** An existing untracked
  file, dangling link, directory, or proposed fragment-name collision
  stops. Never stash, discard, adopt its content, or choose another
  filename to evade the collision. Unrelated siblings stay outside the
  target set and untouched.
- **Repeat the clean-index gate.** Require
  `git diff --cached --quiet` to exit 0 immediately before editing.
  Leave any unexpected staged state intact and stop.

Immediately before the **first changelog edit**, perform *Snapshot
Revalidation*, then recheck the clean index and target safety and
cleanliness. Stop on any change rather than widening the operation.

### Complete patch approval and commit proof

1. **Create only the owned patch.** Edit only the clean exact targets
   and explicitly stage **every** target, including a new single-file
   changelog as well as new fragments. Use literal pathspecs:

   ```powershell
   git --literal-pathspecs add -- <exact-target-files>
   ```

   Inspect `git diff --cached --name-only` without a path filter and
   the **entire staged patch**. Every intended change must be present;
   every actual path and change must be owned. Unexpected additions,
   omissions, or formatting stop, even inside an otherwise owned file.
2. **Approve the complete commit, not just entry text.** Show the full
   staged patch, including all new-file content, modes, deletions, and
   formatting, together with the captured scope and complete proposed
   message. Use a `docs(changelog):` subject, every trailer required by
   `AGENTS.md` and the session, and one `Issue: #N` per distinct issue
   in scope; omit issue trailers when nothing is bound.

   Retain the approved parent OID, index tree from `git write-tree`,
   exact changed paths, target worktree contents/modes, covered
   work/anchors, and exact message including its final newline.
   Obtain explicit approval of all of it before committing.
3. **Recheck immediately before committing.** Repeat the complete-history
   gate before writing pending evidence or making the commit. Require
   the same branch and head, identical index tree and full staged
   changed-path set, unchanged message, and target worktree
   contents/modes identical to the saved approval-time worktree state.
   Their Git content must still match the approved index. Recheck safe
   paths too: `--only` reads worktree files and must not pick up a later
   edit. A failed history gate or any index, path, target, or head drift
   invalidates approval and stops, even with unchanged endpoint OIDs.
4. **Persist pending evidence, then make exactly one commit.** Write
   the approved pending receipt atomically. On failure, do not commit.
   Supply the frozen message unchanged, with no editor rewrite:

   ```powershell
   git --literal-pathspecs commit --only --cleanup=verbatim `
     --file <approved-message-file> -- <exact-target-files>
   ```

   This is the single path-limited `git commit --only` operation for
   all entries, not one commit per entry. Never include another path.
   A failed command or uncertain result stops with pending evidence
   intact; never retry the commit automatically.
5. **Prove the result before completing the receipt.** Require the
   branch to remain captured and its new head to be the created commit.
   Verify its sole parent, tree, exact changed-path set, and **entire
   message including the final newline** against approved evidence.
   Apply *Private Changelog Receipts*'s checks, not a subject test.
   Hook changes or any mismatch prohibit push. Only an exact match
   permits atomic completion of the same receipt with `commitOid`.
6. **Extend only by that expected head advance.** Add the verified
   commit to `R` and `L`, keep `W` and base unchanged, and update the
   captured head to that OID. Show the added publisher commit separately
   and re-echo the complete inventory before publication approval.
   Never recapture a wider range to conceal an unexpected commit.

**No automatic cleanup.** Declining approval or interruption can leave
owned drafts staged; a failed commit proof can leave a local commit.
A failed receipt completion also stops before push. Report exact paths,
receipt state, and any local commit. Never amend, reset, unstage, stash,
discard, or make a compensating commit. The next run must still pass the
original clean-index and clean-target gates.

## Pull Request Content

After the changelog result is proven, or no new commit is needed,
assemble the complete title/body from the **same inventory**. Resolve
`PR opens as draft` through the shared protocol before approval.

### Body assembly

In **slot mode**, build it from, in order:

1. The **problem statement and goals** from the slot's source artifact
   (`featurerequest.md` / `bugreport.md`).
2. The **Approach** section from `plan.md`, as context for the validated
   recorded work, not a claim that unrecorded planned work was published.
3. The complete captured range and its `W` commit list, then a separate
   **Publisher changelog commits** section listing every OID/subject
   in `L`, including a commit just added.
4. `Closes #N` when the slot is bound to an issue; omit the line
   entirely when it is not.

In **branch mode**, build it as:

1. A short **summary paragraph** naming what the whole branch does,
   with the captured base/head identities.
2. **One section per in-scope slot**, ordered by its matched commits.
   Use the source's problem/goals and the plan's **Approach** as
   context, list only validated matching commits, and label partial
   matches explicitly. Do not claim an entire partly matched plan was
   published. Head each section with its issue when it has one.
3. **Other changes**, listing all unmatched work from commit messages.
   When trailer grouping applies, label those groups **trailer-only**;
   never present them as validated slot provenance.
4. **Publisher changelog commits**, listing every commit in `L`
   separately. The work sections and this section must cover all `R`,
   including merges; no unmatched or publisher commit disappears.
5. **One `Closes #N` line per distinct issue** from the existing union
   in *Slot Discovery*, each on its own line. Omit the block when empty.

Never collapse several issues into one `Closes` line or pick a primary
issue and drop the rest. Never read protected artifacts for this body.

### Approval

**Approve scope, PR content, and push before publication.** Show the
complete `R` with work/publisher classifications, repository, current
branch, base/head OIDs, existing PR identity or absence, proposed
create/update operation, full title/body, and proposed non-force push
refspec. Obtain explicit approval and freeze this exact content.
Entry approval or changelog-commit approval is not push approval.

A push can already update an existing PR's commits, so title/body
assembly and review cannot wait until afterward. Corrections require
showing and approving the revised payload before push. This approval
does **not** authorize the later GitHub PR write; that needs its own
in-the-moment confirmation after push and revalidation.

## Push

Immediately before pushing, perform *Snapshot Revalidation*, including
the freshly fetched base and history gate, current branch/head, existing
PR identity or absence, and exact frozen title/body and scope. Any drift
invalidates approval and stops. Require the approved head to remain the
tip of the captured currently checked-out branch, then use that **OID**,
not a mutable `HEAD`, as the source:

```powershell
git push origin <approved-head-OID>:refs/heads/<captured-current-branch>
```

Never a force variant, including a lease-guarded one. Never push another
branch or configure an upstream as a side effect; this protocol does
not depend on one. A rejection or uncertain push result stops with an
honest report, not an automatic retry, branch rewrite, or repair.

## Open or Update the Pull Request

The early PR gate selected the operation; this is **not** the first
lookup, and it cannot repair a wrong-base PR after pushing.

1. **Verify the remote snapshot before separate approval.** Perform
   *Snapshot Revalidation* again. Read the exact remote head ref at
   `origin` and require its OID to equal the approved head, with the
   freshly fetched base still equal to the approved base. Require the
   same PR identity or absence and actual base/head repositories and
   branches. For an existing PR, its remote head must now be the
   approved pushed OID, the only expected remote-head advance.
2. **Obtain separate in-the-moment PR-write approval.** Show the chosen
   create/update target and the same complete frozen title/body.
   Preserve an existing PR's draft state; for creation, show the
   resolved draft policy. Push approval is not this approval.
3. **Immediately revalidate after approval and before writing.**
   Repeat *Snapshot Revalidation*, including its post-fetch history
   gate, and the remote head and exact payload checks. A PR appearing,
   disappearing, retargeting, or becoming ambiguous stops; never
   silently switch operations, retarget it, or create a second PR.
4. **Send exactly the approved title and body.**
   - For the same existing open PR, update it in place:

     ```powershell
     gh pr edit <n> --repo <owner/repo> --title <approved-title> `
       --body-file <approved-body-file>
     ```

   - For confirmed absence, create:

     ```powershell
     gh pr create --repo <owner/repo> --base <captured-base-branch> `
       --head <captured-current-branch> --title <approved-title> `
       --body-file <approved-body-file> --draft
     ```

     Omit `--draft` only when `PR opens as draft` is recorded as `no`.
5. **Read back identities and content.** Verify the resulting PR's
   immutable identity/number/URL, open state, base/head repositories,
   branches and OIDs, draft policy, and complete title/body as JSON.
   Require exact decoded title/body equality, including whitespace and
   final newline, not a rendered approximation. Reconfirm the remote
   head and base against the approved snapshot. On any failed or
   mismatched read-back or uncertain write, report what is known and
   stop without a retry, second create, or automatic repair.

## Report

Report the actual outcome, including a refusal or partial success:

- Repository, mode, branch, captured base/head identities, and PR URL
  when known; distinguish a successful push from a verified PR write.
- The history gate's observed result/error and refusal boundary, if any.
  If history or required traversals could not be verified, say that no
  current verified publication inventory is available. Never present a
  truncated list as verified; label any previously captured inventory
  as earlier evidence with its authorization invalidated.
- The complete `R`, when verified, including merges and old/new publisher
  commits in `L`; matched work, partial associations, trailer-only
  groups, unmatched work, and the distinct issue set. Use the captured
  inventory and name what was actually approved; never infer a new
  subset for the report.
- Changelog targets, whether a commit was added or skipped, receipt
  path/state, and verification evidence or discrepancies.
- Any drafts left staged, nonconforming local commit, prior remote
  effects, or manual recovery needed. Report sentinel-only `AGENTS.md`
  changes as left unstaged. Never claim rollback or atomic publication.

## What This Skill Never Does

- **Never merges** the pull request.
- **Never closes an issue.** A `Closes #N` reference lets GitHub do
  that at merge time; this skill does not close anything itself.
- **Never takes the pull request out of draft.** Marking it ready for
  review is a human signal about human readiness.
- **Never reads or quotes `review.md` or `approach*.md`.** Review
  findings and rejected solution shapes are internal artifacts; they do
  not belong in a public PR body. The prohibition is on the **files** —
  *Body assembly* step 2 lifts `plan.md`'s `## Approach` section into
  the body as it always has, because that section is `dev-plan`'s own
  prose. When `review.md` is **absent** — for the named slot in slot
  mode, or for any in-scope slot in branch mode — *recommend* running
  `dev-review` against it first, naming which slots lack one. A
  recommendation, **never a gate**.

## Important Rules

- **State the mode in your first response.** Slot mode or branch mode,
  and why you picked it. The user gets to correct a mis-chosen scope
  before it becomes a mis-scoped pull request.
- **Today's date governs slot expansion.** Never reuse a previous day's
  `<MMDD>` for a numeric slot. For an earlier slot, the user must give
  a full path.
- **Complete history is mandatory in both modes.** Require the successful
  `false` result from *Preconditions* and complete required traversals
  before trusting an inventory. Repeat the gate at every named boundary,
  even with no new changelog commit or a no-op push. Never bypass refusal
  by switching modes or repairing history; the operator must supply a
  complete-history checkout and re-invoke independently.
- **Both modes publish the complete captured range.** Branch mode
  includes unmatched work, merges, and publisher commits. Slot mode
  requires nonempty normalized `S = W`, disjoint from `L`; it is not a
  subset selector. A wider branch needs a separate explicit branch-mode
  selection and new review, or a branch the user prepares themselves.
- **Receipts classify bookkeeping; they never hide publication.**
  Only exact receipt-backed proof grants `L` status. Keep every such
  commit in the scope preview, PR body, push, and report, even on a
  re-run that needs no new entry.
- **Every issue in scope gets its own `Closes #N`.** Branch mode
  routinely spans several issues; dropping one leaves it open after
  merge.
- **`dev-do` still never pushes and never opens a pull request.** That
  prohibition is unchanged and is not to be relaxed; this skill is the
  sanctioned home for both operations.
- **Slot artifacts are read-only here,** in both modes and for every
  slot discovery turns up. This skill writes no `featurerequest.md`,
  `bugreport.md`, `plan.md`, `review.md`, or `approach*.md`. The
  `Issue` binding belongs to `dev-issue` — if a slot is unbound and the
  user wants it bound, point them at `dev-issue` rather than writing a
  number yourself. Never downgrade or repair a binding here.
- **The single commit owns only exact changelog files.** Clean targets,
  explicit staging of new files, complete patch/message approval, and
  exact parent/tree/paths/message proof are mandatory. All other
  published commits already exist; never create a source-work or repair
  commit here.
- **Private receipts are an additional write surface.** Write only this
  publisher's Git-resolved records under *Private Changelog Receipts*.
  No new setting or slot artifact is involved. Configuration writes
  remain sentinel-only, approved through the shared protocol, and
  unstaged.
- **Refusal never triggers cleanup or a branch rewrite.** Preserve
  staged drafts and failed commit evidence. Never amend, reset,
  unstage, stash, force-push, or manufacture a matching branch.
- **Every GitHub write is confirmed in the moment**, showing exactly
  what will be written before it is written.
- **Never a bare `gh` write.** Use command-specific explicit targets:
  `gh repo view <owner/repo>` takes a positional repository; applicable
  `gh issue`, `gh pr`, and `gh label` commands pass
  `--repo <owner/repo>`; every `gh api` invocation carries
  `repos/<owner>/<repo>/...` in its endpoint. Take every target from the
  `Repository` row after the remote cross-check has agreed with it.
  Never retry against an implicit repository.
- **Report the actual outcome from the verified inventory.** If history
  proof fails, report that refusal, never a truncated range as verified.
  Name the PR URL when known, what was pushed, publisher and work commits,
  partial or unproven associations, issues, receipt state, and any
  side effects left by a refusal. Do not claim a successful PR write
  from push success alone.
- **Honor repo conventions.** Repository conventions live in
  `AGENTS.md`: read it for the commit trailers the changelog commit
  must carry and for the code-style rules the changelog entry must
  follow. If it is absent, fall back to `README.md` /
  `CONTRIBUTING.md` and state which source you used — but note that an
  absent `AGENTS.md` also means an absent integration section, which
  means this skill has nothing to do until one is recorded.
