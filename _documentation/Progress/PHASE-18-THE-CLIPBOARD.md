# Phase 18 — The clipboard

The tasks are in [Phase 18 — The clipboard](../Plan/PHASE-18-THE-CLIPBOARD.md).

**P18.1 brought the clipboard in as a port with one field, and the names as
the first thing put on it.** `Ports.Dependencies` gained `Clipboard`, and the
record holds only `SetText` for now: reading text and the list of files have no
caller until P18.4 and P18.6, and a field nobody calls would have been a second
adapter written blind. The text is a `ClipboardText` in `Domain` — lines that
cannot be empty, so `..` under the cursor with nothing marked never reaches the
port — and the line break is not part of it. The state builds the same lines on
every system; `ClipboardText.joined` takes the break from the adapter, which on
Windows is `Environment.NewLine`. That kept the state free of the platform
without a new port for one constant.

The names come from `PanelState.operationNames`, the operation targets each
under its own name. In a panel of search results that differs from what the
panel shows: the listing names a hit by its path from where the search started
(`a\one.txt`), and the clipboard gets the hit's own name (`one.txt`). Inside an
archive an entry's name is already the name the archive holds it under.

The refusal travels back through the state, as the history's does. `Shell`
answers a refused `PutOnClipboard` with `ClipboardUnwritten`; the transition
reports `ClipboardUnavailable` the first time and records it in
`AppState.ClipboardReport`, and a `ClipboardFailed` — the Win32 clipboard held
by another program past the retries, or refusing the block — is reported every
time. `AppState.HistoryReport` already had the two cases the clipboard needed,
under a type named for the history; the type became `FailureReport` and both
fields wear it.

`SystemClipboard` speaks `user32` and `kernel32` directly: `OpenClipboard` with
up to ten attempts ten milliseconds apart, `EmptyClipboard`, a moveable
`GlobalAlloc` block of UTF-16 with its terminating null, `SetClipboardData` with
`CF_UNICODETEXT`, and `CloseClipboard` in a `finally`. The block belongs to the
system once `SetClipboardData` takes it and is freed by Marbas only on the paths
where it was not taken. On Unix the adapter answers `ClipboardUnavailable`
without trying, which is a row in [Rough edges](../ROUGH-EDGES.md).

The tests: `ClipboardTextTests` for the lines and their spelling — one line
with no break after it, several joined by `\r\n` and not ended by one;
`State/ClipboardTests` for the text built from the cursor entry, a directory,
marked entries in listing order, `..` alone, search results, the inside of an
archive, the marks left in place, and the refusal reported once while a failure
is reported every time; `ShellTests` for the port called and the refusal
dispatched; `KeyMapTests` for both keys; and `ClipboardKeyTests`, which press
`Ctrl+Ins` and `Alt+C` on a `MainWindow` with a fake clipboard and press `Alt+C`
twice against the Unix answer to see one dialog.

The keys were then pressed through a running `Application` with the
`render-tui-screen` probe, over a scratch directory holding `docs`, `notes.txt`
and `readme.txt`, with the real `Dependencies.create` and only the clipboard,
the error dialog and the three configuration files replaced:

```
↓ Ctrl+Ins                        handed: ["docs"]
↓ Alt+C                           handed: ["notes.txt"]
Ins Ins Alt+C                     handed: ["notes.txt"; "readme.txt"]
Unix answer: ↓ Alt+C Alt+C        reported: Clipboard / Marbas cannot reach
                                  the clipboard on this system yet.   (once)
```

The real Windows adapter was then left in place and `↓ Ins Ins Ctrl+Ins` pressed
over the same directory; PowerShell's `Get-Clipboard` read back
`docs\r\nnotes.txt` — two lines, the Windows break between them and none after
the last — so another program sees what the adapter wrote.

**P18.2 copied the full paths, and taught one function to spell a child of
any place.** `Alt+Shift+Ins` and `Alt+P` raise `PathsCopyRequested`; the
transition shares its last step with the names — lines to a `ClipboardText`,
nothing when the list is empty — and differs only in what it reads:
`AppState.operationSpellings`. The ready-made `operationPaths` would not do:
it answers paths on the disk alone, and drops a hit inside an archive, which is
exactly one of the three cases the task names.

The spelling an entry needed already existed twice over — `FindHit.spelled`
for a hit, and `ArchivePath.spelled` beneath a level for a panel inside an
archive — so it became `PanelLocation.spelledChild`, the spelling of a name
inside any place, and `FindHit.spelled` became that function applied to the
hit's own location. A target in a panel of search results is spelled through
its hit; any other through the place the panel shows. An entry inside an
archive therefore comes out as the title spells its level, followed by its name
(`C:\start\backup.zip\docs\inner.txt`), and a directory with no separator after
it, because nothing adds one.

The tests: three in `PanelLocationTests` for a child on disk, at the top of an
archive and on a level inside one; six in `State/ClipboardTests` — the cursor
entry, a directory, marked entries in listing order, `..` alone, a level inside
an archive, and search results holding a hit on disk and one inside an archive;
both keys in `KeyMapTests` and through `MainWindow` in `ClipboardKeyTests`. The
`render-tui-screen` probe pressed `↓ Alt+Shift+Ins`, `↓ Alt+P` and
`Ins Ins Alt+P` through a running `Application` over the scratch directory of
P18.1 and got the full path of `docs`, of `notes.txt`, and of `notes.txt` and
`readme.txt`.

**P18.3 copied where the links lead, and gave Marbas a reader of shortcuts.**
`Ctrl+Alt+Ins` and `Alt+L` raise `LinkTargetsCopyRequested`, read through
`AppState.placesLedTo`. A symbolic link and a junction needed nothing new: the
listing already carries `LinkTarget` for the status line, and .NET reads a
junction's target the same way as a link's. What was new is a relative target
— `ln -s readme.txt near.txt` — which the status line shows as written and the
clipboard now gets whole, through `FileSystemPath.resolvedFrom` the directory
the link is listed in. That directory is the one in the panel, so a link inside
a linked directory resolves under the path the user sees, as the plan wanted.

The shortcut parser is `FileSystem/Shortcut.fs`: header and class identifier,
the shell item list stepped over, then the link information — local path or
network share, UTF-16 when the wide offsets are there — and the relative path
of the string data as the last resort. A path in a code page other than ASCII
is not guessed at; Windows writes the relative path in UTF-16, and that is
where the parser goes instead. Every read is bounds-checked, so a truncated
shortcut or a file that only ends in `.lnk` is no target, and the copy falls
back to the shortcut's own path. The byte builders the tests share live in
`ShortcutBytes.fs`.

The tests: sixteen in `ShortcutTests` — a local path, a base path and suffix,
the item list stepped over, a UTF-16 path with Polish letters, a network share,
a relative path, a Latin-1 path giving way to the relative one, no place at
all, foreign bytes, a truncated file, the `.lnk` name both ways, and on
`MockFileSystem` a full target, a relative one, garbage and a missing file;
two in `FileSystemPathTests` for `resolvedFrom`; eleven in `State/ClipboardTests`
for a link to a file, to a directory, a relative link, a shortcut, a plain
file, a directory, an unreadable shortcut, marked entries, an entry inside an
archive, a shortcut found by a search and `..` alone. On the real disk,
`LinkTargetCopyTests` makes a junction, a relative shortcut below a junction and
a shortcut written by Windows itself through `WScript.Shell`, and all three are
read; the fourth, a relative symbolic link below a linked directory, needs the
right to make symbolic links, so it is skipped on a Windows machine without it
and runs on the Linux runner, where anyone may make one. The probe then pressed
`↓ ↓ Ctrl+Alt+Ins`,
`End Alt+L` and `↑ Alt+L` through a running `Application` over a scratch
directory holding a junction `place` and a shortcut `report.lnk` saved by
`WScript.Shell`, and got the junction's directory, the shortcut's file, and
`readme.txt`'s own path.

**P18.4 read the clipboard back, and taught Marbas to forgive a path.** The
port gained `GetText`, answering the text or `None` when the clipboard holds no
text; the Windows adapter asks `IsClipboardFormatAvailable` before
`GetClipboardData` and copies the `CF_UNICODETEXT` block out under
`GlobalLock`. Because a refusal can now come from reading as well as writing,
`ClipboardUnwritten` became `ClipboardRefused`, and the dialog's sentence for a
failure stopped saying *put*. The environment arrived as
`Processes.EnvironmentVariable`, filled by `HostEnvironment.variable`.

The forgiving is `FileSystem/PastedPath.fs`, a function of the file system,
the variable lookup, the directory relative text starts from and the text:
trim; drop a pair of matching quotes; refuse several lines and blank text; read
a `file:` URI through `Uri.LocalPath`; otherwise give a leading `~` the home
directory (`USERPROFILE` or `HOME`) and expand the variables of the platform —
`%NAME%` on Windows only, because `C:\$Recycle.Bin` is a name there, and
`$NAME` or `${NAME}` on Unix — leaving an unknown one as written, as Windows
does; refuse a character no path may hold; resolve from the directory and drop
a trailing separator, a root keeping its own. The state keeps what only it
knows: the directory the active panel lends (`PanelLocation.holding`), and
what the path names when it arrives — a directory entered, a file stood beside,
nothing passed over without an effect.

The tests: twenty-seven in `PastedPathTests`, three of them for Windows and two
for Unix alone; six in `State/ClipboardTests` for the directory asked for on
disk and inside an archive, a directory, a file, the folder history and a path
that is not there; five in `ShellTests` for a forgiven path dispatched, `~`
through the port, several lines, blank text, an empty clipboard and a refusal;
the Unix adapter refusing to be read; both keys in `KeyMapTests`; and in
`ClipboardKeyTests` a directory, a quoted file with the cursor landing on it,
and a missing path and two lines leaving the panel with no dialog. Then the
real Windows adapter, through a running `Application` with the probe: each
form below was put on the clipboard with `Set-Clipboard`, `Alt+J` pressed over
the scratch directory `start`, and the panel title and cursor read back.

```
  "…\clipwork\work"              → …\clipwork\work
'…\clipwork\start\readme.txt'    → …\clipwork\start, cursor on readme.txt
file:///…/clipwork/work/report.txt → …\clipwork\work, cursor on report.txt
..\work                          → …\clipwork\work
%TEMP%                           → C:\Users\…\AppData\Local\Temp
~\Documents                      → C:\Users\…\Documents
two lines                        → stays in start, nothing said
…\clipwork\nowhere               → stays in start, nothing said
```

**P18.5 let a path run into an archive.** What `PastedPath.read` answers no
longer goes straight to `FileSystemPath.kind`: the state asks
`FileSystem/Landing.fs`, which answers `Into` a location or `Beside` an entry
in one, and the disk is the first case of it rather than a separate branch.
`Landing.ofPath` walks up from the path while nothing stands there, keeping
the names it climbs past. Reaching a directory or a file with no names left is
the plain landing of P18.4; reaching a file with names left over opens it only
when `ArchiveReader.kindOf` says the capability table can read its kind, and
then the last name is looked up in the listing of the level above it — a
directory opens that level, a file opens the level holding it with the cursor
on it, anything else is no landing. The spelling read back is the one `Alt+P`
writes and the panel title shows, so a path copied out of an archive goes back
into it.

The tests: fifteen in `LandingTests` on `MockFileSystem` with a zip written by
`Archive.write` — a directory and a file on disk, the archive itself, two
levels, three entries at three depths, four paths the archive does not hold
(a missing name at the top and inside `docs`, a missing level, a name below a
file), a path through a file that is no archive, an archive no reader can open
and a path below a directory that does not hold it; four in
`State/ClipboardTests` for a level, an entry with the cursor on it, the level
in the folder history and a name the archive does not hold; and one in
`ClipboardKeyTests` pressing `Alt+J` over a `MainWindow`. The probe then put
four paths on the real clipboard over a zip made by `Compress-Archive`: the
`docs` level opened, `docs\inner.txt` opened `docs` with the cursor on it, and
`backup.zip\gone.txt` and `notes.txt\inner` left the panel where it was without
a word.

**P18.6 put the files themselves on the clipboard, after a spike proved the
format.** The spike wrote, from C# loaded into PowerShell, a `CF_HDROP` block —
a twenty-byte `DROPFILES` header with the wide flag set, then the paths in
UTF-16, each ended by a null and the list by one more — and beside it the
registered `Preferred DropEffect` holding a `DWORD`, then asked the Windows
shell to paste into a directory through `Shell.Application`'s `paste` verb, the
same handler Explorer's own Paste runs. An effect of 5 (copy and link, what
Explorer writes) and of 1 (`DROPEFFECT_COPY`) both copied a file and a
directory; 2 (`DROPEFFECT_MOVE`) moved the file out of its directory. The
adapter writes 1 and 2, the documented values.

The port gained `SetFiles`, taking a `ClipboardFiles` — a `ClipboardIntent`
(`ForCopying` or `ForMoving`, in `Domain`) and a list of paths that cannot be
empty, in `FileSystem` beside `FileSystemPath`. The Windows adapter now builds
every block from bytes: the text became a null-ended UTF-16 array like the file
list, and one `settingOnWindows` opens, empties, hands over each block in turn
and closes, for text and files alike.

`Alt+Shift+C` and `Ctrl+Shift+C` raise `FilesOnClipboardRequested ForCopying`,
`Alt+Shift+X` and `Ctrl+Shift+X` the same with `ForMoving`. The transition takes
`transferSource`, so the targets are the ones `F5` would take, search results
split into the files on disk and one group per archive. Files on disk go
straight to the port. An entry inside an archive to be cut is refused with
`ArchiveEntryNotCut` and its spelling; to be copied, the groups go to `Shell` as
`ExtractForClipboard`, which unpacks the archive groups into a fresh
`Workspace.scratch` directory through `planFor` and `Archive.extractAll` under
the `Unpacking` title, and dispatches `ClipboardExtractFinished` with the
scratch directory and the results. The state then hands over the files on disk
followed by what unpacked, each found by `Archive.landedAs` — made public for
it — and reports what failed. The scratch directory stays until Marbas quits,
since another program may paste at any time.

The tests: four in `ClipboardFilesTests`; eleven in `State/ClipboardTests` —
the cursor file to be copied, marked entries to be moved in listing order,
`..` alone, search results on disk, entries inside an archive, a cut inside an
archive refused by name, search results holding a hit in an archive copied and
cut, the unpacked copies handed over after the files on disk, a failure
reported beside the rest, and nothing handed over when nothing unpacked; six in
`ShellTests` for the port called with the intent, a refusal dispatched, the
entries unpacked into `temp/Marbas/session/1`, the results dispatched back with
it, the `Unpacking` title, and a locked archive whose password is not given
left out without a batch; the Unix adapter refusing files; the four keys in
`KeyMapTests` and the error sentence in `OperationErrorMessageTests`; and six in
`ClipboardKeyTests` pressing each key over a `MainWindow`, inside an archive
too, where `Alt+Shift+C` hands over the unpacked copy and `Alt+Shift+X`
reports and hands over nothing.

The keys were then pressed with the probe through a running `Application` with
the real `Dependencies.create` and the real Windows clipboard, and what the
adapter wrote was pasted with the shell's `paste` verb:

```
End Alt+Shift+C          clipboard: start\readme.txt, effect 1
                         pasted: readme.txt copied, start unchanged
↓ Ins Space Alt+Shift+X  clipboard: start\docs, start\notes.txt, effect 2
                         pasted: docs (with inner.txt) and notes.txt moved
End Ctrl+Shift+C         clipboard: start\backup.zip, effect 1
backup.zip: ↓ Enter ↓ Alt+Shift+C
                         clipboard: temp\Marbas\session\1\docs, effect 1
                         pasted: docs with inner.txt, archive unchanged
backup.zip: ↓ Enter End Alt+Shift+X
                         reported: '…\backup.zip\notes.txt' cannot be cut from
                         inside the archive; clipboard unchanged
```

Two things the probe showed are its own and not the application's: a key
raised twice in a row from its timeouts arrives once, so the marks were made
with `Ins` and then `Space`; and a progress window opened from inside a timeout
never finishes its batch — `F5` out of the same zip stalls the same way — so
the archive run swapped `Runners.Batch` for the synchronous one the tests use,
leaving the file system and the clipboard real. A copy and a move pasted into
Explorer itself by hand are left to the user.

**P18.7 pasted the files on the clipboard through the batches of `F5` and
`F6`, and closed the phase.** The port gained `GetFiles`, answering a
`ClipboardFiles` or `None`, and `Empty`. The Windows adapter reads `CF_HDROP`
through `DragQueryFileW` rather than walking the `DROPFILES` block itself, so a
narrow list written by an older program reads as well as a wide one, and takes
the intent from `Preferred DropEffect`: the move bit without the copy bit is a
move, anything else — Explorer's copy writes 5, copy and link — a copy, and so
is an effect that is absent or unreadable. Opening, acting and closing became
one `whileOpen`, shared by every request of the adapter.

`Alt+Shift+V` and `Ctrl+Shift+V` raise `FilesPasteRequested`. The state settles
where the files land before the clipboard is read: the directory of the active
panel, or the level of the archive it stands in, refused by name for a kind
that cannot be changed, and refused over search results through the same
`Receiving` refusal a copy into such a panel meets. `ReadClipboardFiles`
carries that target to `Shell`, which dispatches `ClipboardFilesRead` with the
files, or nothing. A copy is `CopyPlaces` with one `FromDisk` group, exactly
what `F5` sends. A move is `MovePastedFiles`, run through the `movePiece` of
`MovePlaces`, and empties the clipboard after the batch when anything moved
and the clipboard still holds the same files — a move whose every collision
was declined, or a clipboard another program refilled meanwhile, keeps it.

The tests: six in `State/ClipboardTests` — the target in a directory and at a
level of a zip, a tarball refused, search results refused, a copy and a move
read from the clipboard; eight in `ShellTests` — the files dispatched with the
target, no files, a refusal, a move into a directory dispatched back, the
clipboard emptied, kept when nothing moved and kept when it holds other files,
and a move into a zip that leaves the disk; the Unix adapter refusing both new
requests; both keys in `KeyMapTests`; and five in `ClipboardKeyTests` through a
`MainWindow` with a fake clipboard — a copy into a directory and into a zip,
each asking before it replaces `notes.txt`, a move into each that empties the
clipboard, and a clipboard with no files.

The probe then pasted through a running `Application` with the real adapter.
The files were put on the clipboard by a Windows Forms `DataObject` — an OLE
data object, the way Explorer offers ordinary files — with the effect Explorer
writes:

```
work\notes.txt, work\docs, effect 5   Alt+Shift+V in start
                                      start: docs\inner.txt, notes.txt; work unchanged
work\cut.txt, effect 2                Ctrl+Shift+V in start
                                      start gains cut.txt, work loses it, clipboard empty
work\fresh.txt, effect 1              ↓ Enter Alt+Shift+V in backup.zip
                                      zip: seed.txt, fresh.txt; work unchanged
work\moved.txt, effect 2              ↓ Enter Ctrl+Shift+V in backup.zip
                                      zip gains moved.txt, work loses it, clipboard empty
text only                             Alt+Shift+V: nothing pasted, nothing said
```

The zip was made by `Compress-Archive`, and the probe ran its batches
synchronously, as P18.6 explains. Pasting files cut in Explorer itself is left
to a hand trial. Reading the code for this task showed that nothing keeps a
directory from being copied or moved into its own subdirectory — by `F5` and
`F6` as much as by a paste — which is now a row in
[Rough edges](../ROUGH-EDGES.md).

## Appendix

**The full path of `..` became the place the panel shows.** `PathsCopyRequested`
now reads `AppState.fullPathsToCopy`, which answers `PanelLocation.spelled` of
the panel's place when nothing is marked and `..` is under the cursor, and the
operation targets' spellings otherwise; `operationSpellings` became private
behind it. A panel of search results shows no one place, so `..` there still
copies nothing, and the names and the places links lead to are unchanged. The
tests, in `State/ClipboardTests`, replaced the one that proved `..` copied no
path: `..` copies the directory, copies the level inside an archive as the
title spells it, leaves the marked entries alone to be copied, and copies
nothing over search results.

**The Windows adapter was proved against another program.** Twenty-three tests
in `Integration/Clipboard/WindowsClipboardTests.fs` run on the real clipboard,
each against Windows PowerShell 5.1 reading and writing through Windows Forms,
as [Testing](../Architecture/TESTING.md) describes:

- text Marbas writes is read by another program with `\r\n` between the lines
  and Polish letters intact; text another program writes is read whole; text
  written by Marbas reads back; an emptied clipboard holds no text for either
  side; files replace the text;
- files Marbas writes are read by another program in their order, with effect
  1 for a copy and 2 for a move; they read back with their intent, a path of
  more than three hundred characters included; an emptied clipboard holds no
  files;
- files another program offers are read as a copy for the effects 1, 3 and 5
  and with no effect at all, and as a move for 2 and 6; text alone gives no
  files;
- files Marbas puts on the clipboard are copied, and moved, by the paste verb
  of the system shell — a file with Polish letters and a directory with a file
  inside;
- a clipboard held open by another program refuses to be written and to be read
  with `ClipboardFailed` and the system's sentence for access denied.

Two mutations of the adapter were tried to see the tests notice: a narrow
`DROPFILES` header failed five of them, and a move read from any effect with
the move bit failed the theory row for effect 3. Measuring the holder showed
that a process opening the clipboard with `OpenClipboard(NULL)` did not keep a
second process from opening it too, while a window handle did, answering
access denied; the holder uses a window, and the adapter, which opens with no
window, is scheduled to follow it as
[P21.7](../Plan/PHASE-21-THE-LOG-AND-THE-DEBTS.md). `KeptClipboard` puts the text back
after each test, so a local run leaves the user's clipboard text as it found
it.
