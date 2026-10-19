# Phase 25 — Local changes

Draft: written before phase 24 was built, and expected to change with what it
teaches.

Goal: everything that changes the repository without leaving the machine and
without moving a branch is done from the repository tab — staging, unstaging,
discarding, committing, removing and moving tracked files, cleaning, ignoring,
the stash — and `F9` outside a repository can make one.

Five decisions run through the whole phase.

**A command acts on the marked rows, or on the row under the cursor.** As in a
files tab: the marks of the active panel when there are any, the cursor's row
otherwise. In the left panel a directory stands for everything under it. Before
this phase the marks have to work in the repository tab, which is why it opens
with them.

**A row's section decides what a command may do with it.** `F7` refuses a
staged row and `Shift+F7` an unstaged or untracked one; a command refused for
every target says so once, a command refused for some of them acts on the rest
and names them. The refusals are values, worded in `Messages`, as
`ResultsRefusal` is for search results.

**Whatever loses work asks first.** Discard, clean and removing a file from the
disk name what they will lose in the question, the way `F8` names what it
deletes. Nothing else asks.

**A call answers with a status.** Each command is an effect whose answer is
followed by a fresh status at once, rather than left to the watch of P24.7,
which is there for changes made outside Marbas.

**The message of a commit outlives the dialog.** It is kept in the state until
a commit succeeds, so a commit a hook refused, or a dialog closed by mistake,
opens again with the message as it was typed. It reaches git through `-F` and a
file in the session's working directory, never as an argument, so no quoting
and no length of a command line stands in its way.

- **P25.1 Mark entries and rows in the repository tab** — `Ins`, `+`, `-`, `*`
  and the selection keys of a files tab work in both panels; a mark in the
  changes panel belongs to a path and a section, and survives a refresh while
  that row does. *Done when:* marking in each panel, a mark on a path in two
  sections and a mark surviving a refresh are proved, and the marks are
  rendered.
- **P25.2 Stage and unstage with F7 and Shift+F7** — `F7` runs `git add` on
  its targets, `Shift+F7` runs `git restore --staged`; a conflicted row staged
  with `F7` is marked resolved. *Done when:* the arguments for each section
  and for a directory, the refusals and a partial refusal are proved in
  `Update`, and both are tried on a repository in `Integration/Disk`.
- **P25.3 Discard changes with Alt+F7** — an unstaged row runs `git restore`,
  a staged row `git restore --source=HEAD --staged --worktree`, an entry in the
  left panel everything it differs from `HEAD` in; an untracked row is refused
  and pointed to `Alt+F5`. The question lists the files. *Done when:* each
  case and the question are proved, and a discard is tried on a repository.
- **P25.4 Commit with F8** — a dialog with the message, the subject on its
  first line, and *Amend*, *All tracked* (`-a`) and *Allow empty*. *Amend*
  fills an empty message with the last commit's. A refused commit — a hook,
  nothing to commit, an identity git does not know — shows git's standard
  error in the dialog, which stays open. A signing key that asks for its
  passphrase in the terminal is a rough edge until P27 can hand the terminal
  over. *Done when:* the arguments of each option, the message surviving a
  refusal and a closed dialog, and a hook's output are proved; the dialog is
  rendered and driven through a real `Application`.
- **P25.5 Remove and move tracked files with Alt+F8 and Alt+F6** — `Alt+F8`
  runs `git rm`, with *Keep the file on disk* (`--cached`) in its question;
  `Alt+F6` asks for the target as `F6` does and runs `git mv`. An untracked
  target is refused for both. *Done when:* the arguments, the refusals and a
  target outside the work tree refused are proved.
- **P25.6 Clean untracked files with Alt+F5** — `git clean -n` lists what
  would go, and the question shows that list, with *Directories too* (`-d`)
  and *Ignored too* (`-x`); `Enter` runs it without `-n`. On an entry the
  clean is limited to its path, on the root it is the whole work tree. *Done
  when:* the list and the arguments for each option are proved, and a clean is
  tried on a repository.
- **P25.7 Ignore an entry with Alt+F2** — a dialog offers the pattern for the
  entry under the cursor, anchored to its directory and ending in `/` for a
  directory, and the file it goes into: the `.gitignore` of the root, the one
  of the entry's directory, or `.git/info/exclude`. The line is appended
  through `IFileSystem`. A tracked entry is warned about, since ignoring it
  does not untrack it, and *Untrack it* (`git rm --cached`) is offered. *Done
  when:* the pattern, each target file and the tracked case are proved on
  `MockFileSystem`.
- **P25.8 Keep changes on the stash with Shift+F8** — a popup lists the stash
  with its index, branch, message and age; `Ins` pushes, asking for a message
  and *Untracked too*; `Enter` applies, `Alt+P` pops, `Del` drops after asking,
  `F3` shows the entry's diff. An apply that conflicts leaves the conflicted
  rows in the changes panel and says so. *Done when:* the parser of the list,
  each command's arguments and the popup are proved, and the popup is driven
  through a real `Application`.
- **P25.9 Initialise a repository with F9 outside one** — `F9` outside a
  repository asks whether to make one in the panel's directory, and opens its
  repository tab after `git init`. *Done when:* the question, the init and the
  tab opened afterwards are proved, and the message P24.4 gave in its place is
  gone.

Every task updates [the user guide](../USER-GUIDE.md),
[What works today](../Progress/WHAT-WORKS.md), [Git](../Architecture/GIT.md)
and [UI and input](../Architecture/UI-AND-INPUT.md) for what it adds.

## Left for a later phase

- Staging part of a file, hunk by hunk.
- Remembering commit messages in the history of `Alt+F12`.
