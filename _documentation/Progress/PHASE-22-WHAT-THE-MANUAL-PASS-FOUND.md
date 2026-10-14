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

**P22.3 keeps a directory's time through an archive, both ways.** The packing
walk hands each directory's own time to the sink, where it handed `None`; the
zip writer and the zip update put it on the entry, where they dropped it; and
unpacking remembers every directory entry that carries a time and dates the
directories once the archive has been read through. How the pieces fit is in
[Archives](../Architecture/ARCHIVES.md).

`ArchiveDirectoryTimeTests` was written first, twenty-seven tests on
`MockFileSystem`, and twenty-five failed: a directory packed, one below it,
one added to an archive and one already in an archive something else joins,
each in zip, tar.gz and 7z and read back through the index; a directory
unpacked, one below it and one that is the target itself, in the same three;
and a directory out of a tar, a tar.bz2 and the tar.xz of the fixture. The two
that passed say what must not change — an entry with no time, and a directory
made only because a file needed it, are left as the system made them.

Four things went differently from the plan.

- **The mock says only in part why the directories are dated last.** On a disk
  a file written into a directory moves the directory's time.
  `Integration/Disk/DirectoryTimeTests.fs` packs and unpacks a dated tree on a
  real disk in each of the three kinds. The code was changed once on purpose
  to date each directory as it is made: all six of those failed, and five of
  the twenty-seven on the mock.
- **The directories are not sorted from the deepest up.** The plan asked for
  that order. Dating a directory does not touch the one above it, so the order
  among them shows nowhere; they are dated in the reverse of the order they
  arrived in, which is deepest first for an archive that names a directory
  before what is in it, and no sort is paid for.
- **A time that cannot be set fails the target.** The plan did not say what a
  refusal does. Each remembered directory carries the target it was unpacked
  for, and a refusal becomes that target's failure unless it already has one;
  `Fakes.UndatableDirectory` arranges it, and the other targets of the same
  walk stay unpacked.
- **The zip update needed an entry of its own.** `ZipFile.AddDirectory` takes
  a name and nothing else, and `ZipFile.Add` refuses an entry whose sizes are
  not known to be zero — which is how twelve tests of adding to a zip failed
  at the first attempt, with *Entry cannot have any data*. `directoryEntry`
  builds the entry the way the library's own factory does and says its sizes
  are zero.

No test unpacks a rar: the suite holds none, as before, and a rar is read
through the same reader and dated by the same code as the rest.

Packing and unpacking a tree of 1 020 directories and 3 000 files takes what it
took, on NTFS and on ext4, in all three kinds; the dating by itself is 44 ms
for those directories on NTFS and 2.5 ms on ext4. The rows are in
[Measurements](MEASUREMENTS.md#a-directory-dated-as-it-is-packed-and-unpacked-p223-2026-10-13).

4207 tests pass and 22 skip on Windows; 4167 pass and 59 skip on Linux under
WSL.

**P22.4 keeps the moment through a zip.** Every entry Marbas writes into a
zip carries the extended timestamp (`0x5455`) beside the local two-second
field, and a zip is read at the exact moment its entries record — the
timestamp, or the NTFS field (`0x000A`) where a tool wrote that. A file of
February packed in October comes out of 7-Zip, `unzip` and `tar` at the hour
it had, and the zip 7-Zip made is unpacked here at that hour too. How the
pieces fit is in [Archives](../Architecture/ARCHIVES.md).

The spike came first and decided the rest. Zips written with either field,
with both and with neither, their local time an hour off the moment, were
unpacked by every tool there was; and zips those tools made were read back.

| | the extended timestamp | the NTFS field |
|---|---|---|
| SharpZipLib 1.4.2 | writes it; reads it, and puts its UTC where the local time was | writes it; does not read it |
| SharpCompress 0.50.4 and 1.0.0 | reads it, cut to two seconds | does not read it |
| 7-Zip 25.01 on Windows, 23.01 on Linux | reads it | reads it, and writes it — into the central directory alone |
| `unzip` 6.00, and `zip` 3.0 | reads it; `zip` writes it | neither |
| `tar` on Windows (libarchive) | reads and writes it | neither |
| Explorer | neither | neither |

So the timestamp is the field to write: whatever honours anything honours it.
And Explorer honours nothing — it turns the local time back with today's
offset, and wrote the local time of its own zip with it — so the plan's
`04:05:06` out of Explorer is not to be had, and the entry in
[Rough edges](../ROUGH-EDGES.md) is narrowed to that and not gone.

`ZipMomentsTests` was written first, thirty-one tests on `MockFileSystem`.
Twenty-one came before the code and fourteen of them failed: the field on a
file and a directory packed, added and locked with a password, a second the
local time cannot hold kept through a round trip, an entry whose local time
and NTFS field disagree, each entry of several read at its own, a zip with a
comment, one of more entries than the short count holds, and the zip 7-Zip
made, which is checked in beside one `zip` made. Ten more hold what the
reader does with bytes that are no zip.

Five things went differently from the plan, or further than it.

- **No library hands over what 7-Zip wrote.** SharpCompress does not read
  the NTFS field in either release and keeps the raw extra fields internal,
  so `ZipMoments.read` reads the central directory itself: the end record,
  the zip64 locator, the records, the two fields. It is paired with the
  library's entries by their order.
- **The timestamp is read there too.** The library does read it, and hands
  it over cut to the two seconds of the local field — the test of a second
  the local time cannot hold failed on `04:05:06` for `04:05:07`. Both fields
  are therefore Marbas's to read, the finer one first.
- **Adding to a zip moved the times of what it held.** SharpZipLib puts the
  UTC of the timestamp where an entry's local time was and writes that back
  on an update, so after `F5` into a zip every older entry stood off by the
  machine's offset from UTC in the field Explorer reads. It had always done
  that to a zip of `zip`'s; with the timestamp on Marbas's own it would have
  done it to all of them. `restoreLocalTime` turns the entries back before the update begins,
  and a test holds it — one that says nothing on a machine set to UTC, which
  [Testing](../Architecture/TESTING.md) records.
- **The field is nine bytes written by hand, and only between 1970 and
  2038.** SharpZipLib's own type accepts any date and writes four bytes of
  whatever it is given, and 7-Zip and SharpCompress read those bytes as
  unsigned: a file of 1965 came back as one of 2101 from the first and of
  2093 from the second. A reader that takes them as signed ends in January
  2038, so a moment outside the years both readings agree on is packed with
  the local time alone. The tests read the bytes back through SharpZipLib.
- **The published binary was not walked.** *Done when* asks for the fixture's
  file out of 7-Zip and `unzip` at `04:05:06`. A probe ran `Archive.write`,
  `Archive.addEntries` and `Archive.extract` on a real disk on both systems
  and the tools unpacked what it packed, as
  [Cross-platform](../Architecture/CROSS-PLATFORM.md) has it; the keys are
  P22.13's to press.

The step in [The manual pass](../Architecture/MANUAL-PASS.md) that compared
Explorer's extraction with the fixture now compares 7-Zip's, and says what
Explorer will show.

What it costs is in
[Measurements](MEASUREMENTS.md#a-zip-that-keeps-the-moment-p224-2026-10-14):
packing a microsecond an entry, a walk of a zip nothing that shows, a walk
that stops early the reading of the central directory — under a millisecond
and a half in a zip of twenty thousand entries — and unpacking a zip that
carries the field about five microseconds an entry inside SharpCompress,
whoever wrote the zip.

4238 tests pass and 22 skip on Windows; 4198 pass and 59 skip on Linux under
WSL.
