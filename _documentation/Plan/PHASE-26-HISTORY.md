# Phase 26 — History

Draft: written before phase 24 was built, and expected to change with what it
teaches.

Goal: the repository tab shows what happened — the commit graph on `F12`, the
history of a file on `Shift+F12`, who wrote each line on `Shift+F3` — and acts
on a commit chosen there or typed in: cherry-pick, revert, reset.

Four decisions run through the whole phase.

**A log is read as it arrives, never whole.** A repository can hold hundreds of
thousands of commits. `git log` runs in the background with one record per
commit, fields separated by `%x00` and records by `-z`; the window shows the
first screen as soon as it is parsed and keeps reading while the user scrolls,
the way the results window of `Alt+F7` shows hits. `Esc` stops a reading still
running, as it stops a search.

**git draws the graph.** `git log --graph` already lays out the lanes of every
merge; the window takes the graph columns of each line as git wrote them and
draws them in box-drawing characters. Laying lanes out ourselves would repeat
git's own algorithm and disagree with it at the edges.

**A history is a window of its own, as search results are.** The commit graph,
a file's history and blame are full-screen windows with their own key table
and bar, built the way `Ui/Find/ResultsWindow` is: a state every key answers,
rendered where a test can lay it out.

**A commit is chosen in a window, or typed.** The commands that take a commit
act on the row under the cursor of a history window. Pressed in a panel, they
ask for a revision — a hash, a tag, `HEAD~2` — in a field that remembers what
was typed, and `rev-parse --verify <rev>^{commit}` refuses one that names no
commit before anything runs.

- **P26.1 Read the log as it arrives** — the parser of the log records and of
  `--graph` lines, and a port call that hands records back in batches while
  `git log` runs and can be stopped. *Done when:* the parser is proved on
  merges, octopus merges, a root commit, decorations and subjects holding the
  separators, and a stopped reading leaves no process behind on a repository
  in `Integration/Disk`.
- **P26.2 Show the commit graph on F12** — each row the graph, the short hash,
  the branches and tags pointing at it, the subject, the author and the date;
  the current branch's history first, all branches with a toggle. Opening it on
  a large repository is timed. *Done when:* the rows, the toggle and the
  window are rendered and driven through a real `Application`, and the first
  screen of a large repository appears before the log is read whole.
- **P26.3 Show what a commit changed** — `Enter` on a commit lists the files it
  changed with their letters; `F3` there shows that file's diff, `F3` on the
  commit itself the whole of it through `git show`. The key that copies a path
  in a panel copies the full hash. *Done when:* the list, both diffs and the
  copy are proved.
- **P26.4 Show the history of a file on Shift+F12** — the same window, limited
  to the entry under the cursor: a file followed across renames (`--follow`),
  a directory as everything under it. `F3` on a commit shows that path's diff
  in it. *Done when:* a renamed file's history and a directory's are proved
  on a repository.
- **P26.5 Show blame on Shift+F3** — `git blame --porcelain` read in the
  background under the progress window, each line shown with the short hash,
  the author and the date of the commit that last changed it. `Enter` on a line
  opens that commit as P26.3 does. *Done when:* the parser is proved on a
  porcelain output with repeated commits and boundary lines, the window is
  rendered, and blaming a large file is timed.
- **P26.6 Choose a commit by its revision** — the revision field, its history,
  and the check that refuses what names no commit. *Done when:* a hash, a tag,
  a relative revision and a refusal are proved.
- **P26.7 Cherry-pick and revert with Shift+F10 and Alt+F3** — on a commit in
  a window or typed in. A conflict stops the command with its rows in the
  changes panel; `F7` and `F8` finish it, and aborting it waits for P28. *Done
  when:* the arguments and a stop on a conflict are proved, and both are tried
  on a repository.
- **P26.8 Reset the branch with Alt+F9** — a dialog choosing soft, mixed or
  hard; hard asks once more, naming the changed files it will lose. *Done
  when:* each mode's arguments and the second question are proved.

Every task updates [the user guide](../USER-GUIDE.md),
[What works today](../Progress/WHAT-WORKS.md), [Git](../Architecture/GIT.md)
and [UI and input](../Architecture/UI-AND-INPUT.md) for what it adds.

## Left for a later phase

- Searching the log by subject, author or hash inside the window.
- Blame of the line as it was before the commit shown, one step back at a time.
