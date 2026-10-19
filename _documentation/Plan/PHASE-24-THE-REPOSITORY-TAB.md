# Phase 24 — The repository tab

Goal: `F9` in a panel standing anywhere inside a git work tree opens a tab of a
second kind — the repository tab — or brings forward the one already open for
that repository. Its left panel lists the work tree and cannot leave it, each
entry coloured by what git says of it; its right panel lists the changes as
`git status` does. `F3` shows a change as a diff in the viewer, `F4` edits as
it does in a files tab. The phase reads and changes nothing in the repository;
staging, committing, history, branches, remotes and merging are phases 25 to
28, listed at the end.

Seven decisions run through the whole phase.

**Marbas runs the `git` program; it links no git library.** Every call is
`git` with a machine format — `status --porcelain=v2 -z`, `rev-parse`, `diff`
— and its output is parsed. LibGit2Sharp, the one maintained .NET library,
runs no hooks, asks no credential helper, signs nothing and reads no
fsmonitor, so a commit made from Marbas would differ from one made in a
terminal; in its official builds SSH has historically been absent. A
program also leaves the single-file build without a native library per
runtime. The cost is a process per call, which P24.1 measures, and a `git`
that has to be installed: at least 2.23, the first with `restore` and
`switch`, checked once per session.

**Git is a layer of its own, and every call is an effect.** `Git/` holds the
types — a status entry, its section, a repository root — and the parsers, pure
functions from bytes to values, tested on strings. It sits between `Messages`,
which words `GitError`, and `Find`, and uses `FileSystem`, `Domain` and
`Collections` alone. Running the program is a port, `Ports.Git`, implemented by
`Adapters/GitProcess.fs`; a transition never waits for git, it raises an effect
and handles the message the answer comes back in.

**The program is a setting.** `Setup` gains a *Git program* field, written as
`git.program`; empty means `git` found through `PATH` as
`ProgramResolution` finds any program. `Alt+U` fills it from the executable
under the panel cursor, as it fills the editor. `Alt+O` opens `Setup` from
every tab, because the repository tab gives `F12` to the commit graph.

**Every call is made the same way.** `-C <root>`, `-c core.quotepath=off` and
`-z` wherever git offers it, so a name comes back as its bytes, unquoted;
`GIT_OPTIONAL_LOCKS=0`, so a status in the background never takes the index
lock from a `git` the user runs at the same time; `GIT_TERMINAL_PROMPT=0` and
`GIT_EDITOR=true`, so git never writes a question into the terminal the panels
are drawn in nor starts an editor there. A failure carries the exit code and
the standard error as git wrote them.

**A tab is a files tab or a repository tab.** `Tab` becomes a union. A
repository tab carries its root, the left panel, the right panel and the active
side; its identity is the root as `rev-parse --show-toplevel` answers it, so
`F9` anywhere inside a repository open in a tab switches to that tab, and a
linked worktree, having a root of its own, gets a tab of its own. The session
file keeps both kinds. `Alt+F11` on a repository tab opens a files tab where
the left panel stands. `F9` refuses a panel inside an archive or in search
results, and a bare repository, which has no work tree to list.

**One status answers a whole refresh.** The left panel colours every entry from
one `git status` held as a map from path to state; a listed entry costs one
lookup, never a call. A directory takes the strongest state of anything below
it. `--untracked-files=normal` and `--ignored=matching` report an untracked or
ignored directory as one row, so the panel decides once per listing whether
the directory it lists lies inside one, not once per entry. An ignored entry is
hidden as a hidden one is, and shown by the same toggle; a file deleted from the
work tree is listed as a ghost in the deleted colour.

**A path can stand in the right panel twice.** A row is a path and a section —
staged, unstaged, untracked, conflicted — so a file changed in the index and
again in the work tree has two rows. Each row carries git's letter for it (`M`,
`A`, `D`, `R`, `U`, `?`) beside the colour of its section, because a colour
alone says nothing on a terminal without colours; a rename reads
`old → new`.

- **P24.1 Measure git calls and the keys of the repository tab** — a probe
  times `git status --porcelain=v2` and the start of `git` itself on a small
  repository and a large one, on Windows and under WSL, and `_probes/KeyProbe`
  asks every key phases 24 to 28 bind — `F9`, `Shift+F3` … `Shift+F12`,
  `Alt+F2`, `Alt+F3`, `Alt+F5` … `Alt+F9`, `Alt+O` — in Windows Terminal,
  the Windows console, cmder and gnome-terminal under WSL. A key that does not
  arrive gets a spare, chosen the way P22.7 chose them. It also tries
  `TerminalHandOver.lend` on Windows, which P27 needs. *Done when:* the times
  are in [Measurements](../Progress/MEASUREMENTS.md), what arrives where is in
  [Cross-platform](../Architecture/CROSS-PLATFORM.md), and the key table of the
  repository tab, spares included, is written down.
- **P24.2 Add the Git layer and its parsers** — `Git/` with the status
  entry, the section, the repository root, the version and `GitError` (not
  installed, too old, not a repository, a bare repository, failed with its code
  and its standard error), and the parsers of `status --porcelain=v2 -z`, of
  `rev-parse` and of `git --version`. The layer gets its row in §1 of [Coding
  style](../coding-style-conventions.md#1-layers), in
  `Layers/LayerReferenceTests` and in the chain `CLAUDE.md` and
  [Overview](../Architecture/OVERVIEW.md) spell out. *Done when:* the parsers
  are proved on an ordinary change, a rename, a copy, each kind of conflict, a
  submodule, an untracked and an ignored directory, and names holding spaces, a
  newline and characters outside ASCII.
- **P24.3 Add the git program setting and the git port** — `git.program` in
  the settings and its field in `Setup`, filled by `Alt+U`; `Alt+O` bound
  beside `F12`; `Ports.Git` and `GitProcess`, which resolves the program, sets
  the environment above, checks the version on its first call, and writes long
  output — a diff — to a file as it arrives rather than into memory. Every
  call is logged under `--debug`. *Done when:* the field round-trips through
  the settings file, `Alt+U` and `Alt+O` are driven through a real
  `Application`, and the adapter is proved in `Integration/Disk` against a
  repository a test makes with `git init`, including a missing program and a
  directory outside any repository.
- **P24.4 Add the repository tab and open it with F9** — `Tab` as a union,
  the session file keeping both kinds and reading a file without the kind as
  files tabs, the `F11` row of a repository tab, `Alt+F11`, `Alt+Shift+F11` and
  `Alt+←` / `Alt+→` over both kinds, the bar of the repository tab built
  from its own key table. `F9` finds the root, switches to the tab holding it
  or opens one after the current tab, and refuses an archive, search results, a
  bare repository and a directory outside any repository — the last with a
  message, until P25 adds `init` and P27 `clone`. *Done when:* opening,
  switching to the tab already open, refusing and a session with both kinds
  surviving a restart are proved in `Update` and the settings tests, and the
  tab is rendered.
- **P24.5 List the work tree with its status in the left panel** — the left
  panel lists the work tree with no `..` at the root, sends `Ctrl+4` to the
  root of the repository rather than of the drive, colours entries and
  directories from the status map, lists ghosts and hides ignored entries
  behind the hidden toggle. *Done when:* each colour, a ghost, a directory
  inside an untracked one and the toggle are proved on a status the test
  writes, the panel is rendered, and listing a large directory is timed before
  and after.
- **P24.6 List the changes in the right panel** — the right panel lists the
  rows by section and then by path, with the letter and the colour of the
  section; the cursor stays on its path and section across a refresh, or on the
  nearest row when that one is gone. *Done when:* the order, a path in two
  sections, a rename and the cursor surviving a refresh are proved, and the
  panel is rendered.
- **P24.7 Refresh the repository tab when the repository changes** — the
  tab watches its work tree and the `index`, `HEAD` and `refs` under the git
  directory, collapses a burst as the directory watch does, and asks for one
  status per burst in the background. An answer older than the last question is
  dropped. *Done when:* a change made outside Marbas, a commit made in a
  terminal and a stale answer arriving after a fresh one are proved, and a
  checkout of many files is timed.
- **P24.8 Show a change as a diff on F3 and edit on F4** — `F3` on an
  unstaged row diffs the work tree against the index, on a staged row the index
  against `HEAD`, on an untracked row shows the whole file as added, and in the
  left panel diffs the entry, or everything under a directory, against `HEAD`;
  an entry with no change opens in the viewer as in a files tab. The diff is
  written into the session's working directory and opened in the viewer, which
  colours added, removed and hunk lines. `F4` edits as in a files tab and
  refuses a ghost. *Done when:* each choice of what is diffed is proved on the
  arguments the effect carries, the colours are rendered, and a diff of a large
  file opens no slower than the file itself.

Every task updates [the user guide](../USER-GUIDE.md),
[What works today](../Progress/WHAT-WORKS.md) and a new
[Git](../Architecture/GIT.md) in the architecture, and
[UI and input](../Architecture/UI-AND-INPUT.md),
[Tabs](../Architecture/TABS.md) and
[Settings persistence](../Architecture/SETTINGS-PERSISTENCE.md) for what it
adds to them.

## Left open

- **Which keys of a files tab keep working in a repository tab.** Navigation,
  `Ctrl+R`, the hidden toggle, `Ctrl+Q`, `Alt+O` and the tab keys work in P24,
  and the marks in P25.1; the rest — search by typing, `F2`, `Alt+F7`,
  `Alt+F12` — wait for a decision.

## The phases after this one

- [Phase 25 — Local changes](PHASE-25-LOCAL-CHANGES.md)
- [Phase 26 — History](PHASE-26-HISTORY.md)
- [Phase 27 — Branches and remotes](PHASE-27-BRANCHES-AND-REMOTES.md)
- [Phase 28 — Merging](PHASE-28-MERGING.md)
