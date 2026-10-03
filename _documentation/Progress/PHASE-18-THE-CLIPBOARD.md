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
