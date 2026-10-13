# Phase 22 — What the manual pass found

The tasks are in
[Phase 22 — What the manual pass found](../Plan/PHASE-22-WHAT-THE-MANUAL-PASS-FOUND.md).

**P22.1 let the bottom row of the user menu be removed.** `fill` in
`UserMenuDialog` read the cursor, replaced the list and put the same index
back. After `Del` on the bottom row that index is one past the end, after `Del`
on the only row there is no index at all, and `ListView.SelectedItem` throws an
`ArgumentException` on either — which, raised from a key handler, ended the
session. The refill now asks `UserMenuState.cursorWithin` for the row: the
entry at the index the cursor had or the next one below it, else the last
entry, and no row when the menu holds no entry. A line is never the answer, so
a menu left ending in a line puts the cursor on the entry above the line.

Three tests in `UserMenuDialogTests` were written first and failed on that
exception: the bottom row removed with an entry right above it, the bottom row
removed under a line, and the only row removed. They pass now, the cursor on
the row above, on the entry above the line and on no row.

Two things went differently from the plan.

- **No other popup shares the shape.** The plan had the history and the
  settings read for it and given the same two tests where they have it. The
  history popup refills a `TableView` and then stands on `min row last`, on
  nothing when no row is left, and `HistoryDialogForgetTests` already forgets
  the last row and the only one. The settings popup never puts a cursor back:
  `SetSource` leaves a list with no cursor, which two throwaway tests
  confirmed by removing the row under the cursor at the bottom of one list and
  as the only row of the other, and pressing `↓`, `↑` and `End` afterwards.
  The favourites popup does put its index back, into a list that always has
  ten rows. The popups of tabs, drives, code pages and handlers fill their
  list once. So the two tests exist for the user menu alone.
- **What the popup remembers moved into one record.** [Coding style](../coding-style-conventions.md)
  holds `UserMenuDialog` to its rule on what a view keeps the next time the
  view is changed, and this was that time. The menu and the entry chosen were
  two settable properties of `UserMenuView`; they are `UserMenuState`, one
  record behind one `let mutable`, changed through `Change` as
  `ResultsView` is. The decisions the handlers made — which row holds an
  entry, which entry a hotkey names, where a step lands, where the cursor
  stands after a refill — are functions of that record and the cursor the list
  reports, in `Ui/Dialogs/UserMenuState.fs`, compiled before the view and
  opening no `Terminal.Gui` namespace. `changed` used to work out the nearest
  entry a second time after `fill` had put the cursor back; it now only moves
  the cursor to the entry the change was about, when there is one.
  `UserMenuStateTests` holds one test per rule, thirty for these, and the
  twenty-five tests that press keys on the real popup pass unchanged.

The screen was read with the `render-tui-screen` probe, through
`Prompts.chooseFromUserMenu` — the real adapter, with its real question — over
a menu of two entries: `↓`, `Del` and *Remove entry* leave one row and a popup
one row shorter; `Del` and *Remove entry* again leave an empty list; `↓`,
`Enter` and `Del` on the empty menu do nothing, and `Esc` closes it with
nothing chosen.

**A second defect of the same refill was closed with it.** It was not in the
plan: it was seen in the running application once the first was fixed. An
entry added to a menu of one was shown alone, the first entry out of sight
until `↑` was pressed, as if the new one had replaced it. The popup grows with
its menu, but the list gets its new height from the layout pass after the key,
and the cursor was put on the new row while the list was still one row tall —
so the list scrolled down to it and kept the scroll when it grew. A test in
`UserMenuDialogTests` adds an entry to a menu of one, lays the popup out as the
loop would and failed on a list shown from its second row.
`UserMenuState.topRowWithin` now answers the row a list of a given height may
start at, and `fill`, which already runs when the list's frame changes, sets
the viewport to it. Eleven tests in `UserMenuStateTests` hold the rule, and the
probe shows both entries after `Ins`, where it showed one before.

4159 tests pass and 16 skip on Windows.

**P22.2 reads the executable bit on Linux.** The listing a panel shows asks
`fs.File.GetUnixFileMode` for every file on Unix, and `LaunchProcess` for the
one file it is about to start; both used to ask `IFileSystemInfo.UnixFileMode`,
which the wrapper of System.IO.Abstractions never fills. A file with the bit is
drawn as a program, started by `Enter` and, on `Alt+Shift+R`, refused with the
platform's reason — seen with the published `linux-x64` binary driven in tmux
under WSL, where the script it started left its file in the panel.

The tests came first. `Integration/Disk/ExecutableBitTests.fs` gives a real
file the bit and lists it; four of its six tests failed on the Linux leg —
the program, the link to it, the link to nothing and the start described —
and all six are skipped on Windows. Beside them the two halves are tested on
the mock on either system: `DirectoryListing.readOn` takes the platform,
`read` hands it the one its file system has, and `Fakes.ModeWitness` records
which paths were asked for their mode. `UnixFact` takes a reason, as
`WindowsFact` does.

Four things went differently from the plan, or further than it.

- **No release of the package passes the question on.** 22.3.0 is the newest
  and answers `None` as 22.2.0 does: `UnixFileMode` is an auto-property of
  `FileSystemInfoBase` that the `FileInfo` wrapper does not override, on the
  main branch as well. A probe under WSL read the same file through the
  runtime's `FileInfo`, through the wrapper and through
  `File.GetUnixFileMode`, with either release. So the mode is asked for
  directly, which is a second `stat` per file.
- **The price was paid by a question nobody had timed.** Taken apart, a
  listing turned out to spend most of its time on `FileSystemInfo.LinkTarget`,
  which goes to the system for every entry, link or not: a fifth of a listing
  on ext4, more than two fifths on a Windows drive mounted into WSL, and 98 %
  on NTFS, where ten thousand files took 560 ms to list and take 26.
  `FileSystemPath.linkTargetOfInfo` now asks only for an entry whose
  attributes say `ReparsePoint`. The plan had the task stop if the mode cost
  out of proportion; with the link no longer asked the listing costs 1.4 ms
  more in ten thousand files on ext4 and a seventh more on the mount, and
  that was taken as in proportion. The tables and the reasoning are in
  [Measurements](MEASUREMENTS.md#a-listing-that-asks-for-the-mode-and-not-for-the-link-p222-2026-10-13).
- **Only the listing on screen asks.** `entryFromInfo` builds the entries of
  every walk as well — copying, deleting, measuring, searching, packing — and
  a `stat` per file there is what the rule forbids. It asks nothing, and since
  what it used to read never answered on a real disk, nothing is lost by it:
  a walk knows a program by its name, as before. A program found by a search
  is therefore still drawn as a plain file on Linux, which is an entry in
  [Rough edges](../ROUGH-EDGES.md) now.
- **A mode that cannot be read does not fail the listing.** The probe showed
  `GetUnixFileMode` throwing for a link that points at nothing and for a file
  deleted since it was enumerated, where the questions asked before did
  neither. Such a file is listed as a plain one; a link to a program is a
  program, since the question follows the link. That is a `with _ ->` of the
  kind [Coding style](../coding-style-conventions.md) keeps for an entry a
  listing steps over, and both cases have a test, on the mock and on the
  disk. The launch does not catch: a file it cannot ask about is reported as
  one that could not be started.

One consequence was seen and left. A Windows drive under WSL marks every file
executable, so there every file is now a program, and `Enter` on a document
tries to start it and reports *Exec format error* instead of handing it to
`xdg-open`. That is what the bit says and what `ls` shows; it is an entry in
Rough edges.

4174 tests pass and 22 skip on Windows; 4134 pass and 59 skip on Linux under
WSL.
