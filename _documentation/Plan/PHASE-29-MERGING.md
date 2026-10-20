# Phase 29 — Merging

Draft: written before phase 25 was built, and expected to change with what it
teaches.

Goal: `F9` merges a branch into the current one, `F10` rebases the current one
onto a branch, and an operation stopped on a conflict — a merge, a rebase, a
cherry-pick, a revert — is resolved, continued, skipped or aborted from the
repository tab.

Three decisions run through the whole phase.

**The tab knows when an operation is in progress.** The status of P25 is read
together with what the git directory holds — `MERGE_HEAD`, `CHERRY_PICK_HEAD`,
`REVERT_HEAD`, `rebase-merge/`, `rebase-apply/` — so the tab knows which
operation stopped and, for a rebase, at which step of how many. The panel
title says it: `MERGING`, `REBASING 3/7`.

**While one is in progress, three keys change meaning.** `F9` continues,
`F10` aborts and `Shift+F10` skips — the last for a rebase and a cherry-pick
only — and the bar says so. Continuing is refused while a conflicted row is
left. A merge, a cherry-pick and a revert continue through the commit dialog
of P26.4, filled with the message git prepared.

**git opens no editor.** P25 sets `GIT_EDITOR=true` for every call, and this
phase adds `--no-edit` wherever git offers it, so a merge or a continued rebase
takes the message git wrote rather than waiting for an editor nobody sees.

- **P29.1 Show the operation in progress** — reading the git directory into the
  operation and its step, the panel title, and the bar's three keys. *Done
  when:* each operation is proved from a git directory the test writes on
  `MockFileSystem`, and the title and the bar are rendered.
- **P29.2 Merge with F9** — a branch chosen from the list of P28.5, with
  *No fast-forward* and *Squash*. A merge that conflicts stops with its rows
  in the changes panel. *Done when:* the arguments, a clean merge and a
  conflicting one are proved on a repository.
- **P29.3 Rebase with F10** — onto a chosen branch, with *Stash the changes
  first* (`--autostash`). *Done when:* the arguments, a clean rebase and one
  that stops are proved on a repository.
- **P29.4 Resolve a conflict** — `Enter` on a conflicted row offers *Edit*,
  *Take ours* and *Take theirs*; the last two run `git checkout --ours` or
  `--theirs` and stage the file. `F7` on a file still holding conflict markers
  asks before staging it. *Done when:* each choice and the question over
  markers are proved.
- **P29.5 Continue, skip and abort** — the three keys for each operation, the
  refusal while a conflict is left, and the commit dialog filled with git's
  message. *Done when:* each key is proved for each operation it applies to,
  and a rebase is taken from its first stop to its end on a repository.

Every task updates [the user guide](../USER-GUIDE.md),
[What works today](../Progress/WHAT-WORKS.md), [Git](../Architecture/GIT.md)
and [UI and input](../Architecture/UI-AND-INPUT.md) for what it adds.

## Left for a later phase

- Interactive rebase.
- Starting the merge tool git is configured with.
- The commit message written in `$EDITOR` rather than the dialog.
- Submodules: `Enter` on one opening its own repository tab, and
  `git submodule update --init`.
