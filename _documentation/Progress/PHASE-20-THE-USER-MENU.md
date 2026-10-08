# Phase 20 — The user menu

The tasks are in
[Phase 20 — The user menu](../Plan/PHASE-20-THE-USER-MENU.md).

**P20.1 made `F4` start the editor without waiting for it, on Windows.**
`EditorProcess` was split into a description and a run. `describe` answers an
`EditorStart` — the `ProcessStartInfo` and an `EditorWait` — and `run` starts
what it describes and, only when told to, waits. On Windows the wait is
`StartsDetached` and the start goes through the shell (`UseShellExecute`), the
same verb `Enter` uses, which is what gives a console editor a console window
of its own instead of the terminal the panels are drawn in. On Unix the wait is
`WaitsForExit` and the editor is started directly as before: a console editor
such as `vi` has no window to open there, so the panels hold still until it
ends and the screen is repainted afterwards. The choice is the platform's and
nothing else's — the editor named for the extension, the default one and
`$EDITOR` are resolved exactly as they were.

The panel is no longer re-read when the editor reports back. `EditorStarted Ok`
leaves the state as it is; until now it ran the refresh `Ctrl+R` runs, which
had a meaning only while the editor's end was the moment `F4` returned. A
detached editor has no such moment, and the directory watch of phase 12
already follows the file as it is saved — on Unix too, so the waiting branch
did not keep the refresh either.

Two names changed with the behaviour. The message `EditFinished` became
`EditorStarted` and the port `Processes.RunEditor` became
`Processes.StartEditor`: on Windows nothing finishes before the answer comes
back, and a name that promised it would have lied on the platform the change
was made for.

The tests: `EditorProcessTests` prove the description on each platform — no
waiting and the shell on Windows, waiting and a direct start on Unix — along
with the file handed over, the directory it starts in and the editor chosen for
the file coming before `$EDITOR`; nothing in them starts a process.
`UpdateTests` prove a started editor changes no state and asks for nothing, and
`EditKeyTests` that `F4` leaves the panel as it was read even when the editor
wrote beside the file before answering. That a file saved by hand shows in the
panel, and that a console editor opens a window of its own, are checked by
running the application.

**P20.2 gave the user menu its entries and its file.** Three domain types carry
what an entry is made of — `MenuHotkey`, one letter or digit kept in capitals;
`MenuLabel`; and `CommandLine`, still unparsed — with `MenuRun` naming the
three ways of starting it. `FileSystem/UserMenu.fs` holds `MenuCommand`,
`MenuItem` (a command or a separator) and `UserMenu`, whose list is private so
that no menu with two commands on one hotkey can be built, and the module that
reads, applies and writes `user-menu.conf` beside the settings.

The four operations — add after an entry, change, remove, move up or down — are
`MenuChange` values applied to the file as it is on disk, the way the history
and the favourites apply theirs. What made them more than a copy of the
favourites is that they name an entry, and a position would name the wrong one
once another copy of Marbas had added or removed something above it. A command
is therefore named by its hotkey, which is unique, and a separator by the
command above it and the separators between them; `UserMenu.refAt` turns a row
of the popup into that name. Two separators in a row are what keep the count:
without it the lower one could not be moved down on its own.

Two refusals came out of it, `HotkeyTaken` and `CommandGone`, carried by
`MenuRefused` beside the file errors in `UserMenuError`; everything else that
finds its entry gone does nothing, since what it asked for is already true.
`Elevation.text` and `Elevation.tryParse` moved out of `History` into a module
beside the type, so the history and the menu spell `user` and `administrator`
in one place. Nothing reads the menu at startup yet and nothing in the
interface reaches it — `F2` and the path on `Dependencies` come with the popup
in P20.4 — so the application starts exactly as it did, and a menu never
written loads as an empty one.

The tests: `MenuHotkeyTests`, `MenuLabelTests` and `CommandLineTests` prove what
each type accepts and refuses. `UserMenuTests` prove each operation — at the
top, after an entry, after one that is gone, a separator; a change in place,
to a new hotkey, to a taken one, to a gone one; a removal and a removal of
something gone; a move each way, at either edge and inside a run of
separators — the round trip and the exact lines of the file, the fallbacks of
the way of running and the elevation, an entry that costs only itself, a
duplicate hotkey and gaps in the numbering; and on `MockFileSystem` a change
on top of a file another copy wrote, a separator still found after an entry was
added above it, a refused change that leaves the file alone, a first change
that creates the directory, and a file that cannot be written or read.

**P20.3 expanded a command line against the two panels.**
`State/CommandExpansion.fs` is one public function, `expand`, from the file
system that spells paths, the path the list file will have, the `CommandLine`
and the `AppState` to an `ExpandedCommand` — the program, its arguments and the
entries the list file must hold, if the line asks for one — or to an
`ExpansionRefusal`. It sits in `State` because it reads `PanelState` the way
the operations do: the marks, the cursor standing in for them, a hit's own
name and path in a panel of search results, and the place inside an archive
that refuses a run.

The line is split into words before anything is put in, and nothing put in is
split again. That is what keeps a path with a space or a quote in it one
argument without any escaping at all, and why `%s` can hand several entries to
one start of the program: it is the only placeholder that makes more than one
word, and text joined to it goes to the first entry and to the last. Each word
remembers whether it was quoted when typed or holds something put in, so the
run through the shell in P20.4 can quote every path as the plan asks while
leaving a bare `|` or `>` to the shell; until then nothing reads that mark.

Three choices the plan left open were made here. `..` under the cursor with
nothing marked is no entry, so `%n`, `%p`, `%s` and `%f` refuse rather than
hand a program nothing or the parent's name. A panel of search results has no
directory, so `%d` there refuses instead of guessing at the place the search
started from. `%f` lists the active panel's entries, the ones `%s` would put
in, since the plan names no side for it; `%Lf` and `%Rf` are therefore not
placeholders and are kept as typed, like any other `%` the notation does not
know — including `%N`, as the letters are matched exactly.

The tests, all in `CommandExpansionTests` on `MockFileSystem`: every
placeholder on the active panel and on each side named, the bare ones following
the active panel when it changes, several entries marked on either side, the
program started once with all of them, text joined to `%s`, names holding a
space, a single quote and — on Unix, where a file may be called that — a double
quote, words typed in quotes, `%%`, seven spellings of a `%` that means nothing,
the list file and a line without one, each placeholder on a panel inside an
archive and the other panel still usable, a panel of search results with its
own names and paths, its marked hits, its missing directory and a hit inside an
archive, the parent under the cursor, and a line that names no program or names
one with a placeholder.

**P20.4 put the menu on `F2` and ran what it chooses.** `F2`, labelled `Menu`
on the bar, raises `UserMenuRequested`; `Shell` reads `user-menu.conf` afresh
and hands it to the popup, and the entry chosen comes back as
`MenuCommandChosen` with the path the list file of `%f` will have — the first
free `marked-N.txt` in the session's working directory. The transition expands
the line with P20.3's `expand` and answers `RunCommand` of a `CommandRun`, or
reports the refusal; `Shell` hands the run to `Processes.RunCommand` and
`CommandFinished` brings the outcome back, remembered on the history when it
started, silent when the elevation was turned down, reported under *Cannot run
command* otherwise.

`UserMenuDialog` is built the way the favourites popup is: a `ListView` whose
`KeyDown` runs before the list's own keys. A bare letter or digit runs its
entry at once, `↑`/`↓` step from command to command over the separators, and
`Enter` runs the one under the cursor; both answer the `MenuCommand`, so a
letter and `Enter` raise the same run. The popup was rendered and driven
through a real `Application` with the `render-tui-screen` probe — a letter,
the arrows over the separator followed by `Enter`, and `Esc` — before any of
this was trusted:

```
╔╡User menu╞═══════════════════════════════════════════════════════════╗
║                                                                      ║
║ E  Edit in vim                                                       ║
║ C  Compare the panels                                                ║
║ ──────────────────────────────────────────────────────────────────── ║
║ Z  Pack the marked files                                             ║
║                                                                      ║
║ A letter or Enter: run    Esc: close                                 ║
╚══════════════════════════════════════════════════════════════════════╝
```

The launch adapter is `Adapters/CommandProcess.fs`, split as `LaunchProcess`
is into a `describe` the tests read and a `start` that writes the list file and
starts. On Windows each way goes through the shell verb, so a console program
gets a window of its own: the program with its words as arguments, or
`cmd.exe /s /c "…"` and `/s /k "…"` with every quoted word in double quotes,
and `runas` for an entry run as administrator — with `cd /d` in front of a
shell line, which was added after the first draft because an elevated start is
not trusted to keep its directory. On Unix the program or `/bin/sh -c` is
started with its streams drained, words quoted in single quotes, and
elevation is refused by platform.

The history had to learn a second kind of start. The types of a run moved
down from `State` into `FileSystem/CommandRun.fs` — `CommandWord`,
`ExpandedCommand`, now carrying a `ListFile` of the path and the entries, and
`CommandRun` — because `history.conf` stores them. `History.Launches` became a
list of `Started`, a file start or a command run, so the *History* tab lists
both in one order; `HistoryItem` gained `Ran`, `RunAgain` carries a `Started`,
and a run is written under the `run.` keys with a `kind` part a file start
never had, so older files read as they did. `[Go to]` on a run stands the
panel in the directory it ran in. `MenuRun.text` and `tryParse` moved into the
domain beside the type, shared by the menu file and the history file.

The tests: `CommandProcessTests` describe each way on each platform, the
`runas` verb and the `cd /d`, the refusal on Unix, and the list file written or
refused on `MockFileSystem`; `HistoryTests` a run remembered, deduplicated,
forgotten, written, read back and passed over when a part is missing or a word
breaks a line; `UserMenuRunTests` the round trip through `update`, the working
directory inside an archive, a refusal, the three outcomes and a run again from
the history; `ShellTests` the menu handed to the popup with and without a file,
an unreadable file, the list file named, the run handed to its port and the two
reports; `UserMenuDialogTests` the rows, where the popup opens, a letter in
either case, `Enter`, a letter nothing holds, the arrows over a separator and at
the end, and an empty menu; `UserMenuKeyTests` `F2` through the main window to
the port and onto the history file, a refused run and a closed menu;
`KeyMapTests`, `UserMenuRowTests`, `HistoryRowTests`,
`ExpansionRefusalMessageTests`, `UserMenuErrorMessageTests` and
`WorkspaceTests` the pieces. Each way of running is to be tried by hand.

**P20.5 made the menu editable where it is shown.** `UserMenuDialog.create`
now takes a `MenuEditing` — `AskEntry`, `AskRemoval` and `Change` — and the
list's `KeyDown` answers five more keys from `KeyMap`: `Ins` adds, `F4`
changes, `Del` removes, `Ctrl+↑`/`Ctrl+↓` move. `Change` is `Shell`'s
`changeUserMenu`, `UserMenu.change` on the menu's path, so every change is read,
applied and written at once, a failure is reported under *User menu*, and the
popup redraws from the menu the file then holds, growing or shrinking with it
and keeping the cursor on the entry the change was about.

`MenuEntryDialog` is the popup `Ins` and `F4` open: label, hotkey, command
line, the three ways of running and *As administrator*. `MenuEntryForm.read`
reads the fields against the other entries and refuses a blank label, a hotkey
that is not one letter or digit, a hotkey another entry holds — naming that
entry — and a blank command line; the popup checks it in `OnAccepting` and
stays open with the reason under the box, which is how "a duplicate hotkey is
refused in the dialog" was met rather than by letting the file refuse it
afterwards.

One thing the plan left unanswered was settled with the user: the cursor steps
over separators (P20.4), so a separator could never be under it to be removed
or moved. A line is now edited from the entry beside it. `Ins` offers *an
entry*, *a line above* or *a line below* the entry under the cursor, the fields
greyed out for a line; `Del` asks — through a `MessageBox` with a button per
choice and *Cancel* last, the one it opens on — whether to remove the entry or
the line right above or below it, whichever of those is there, as
`MenuRemoval.offered` answers; and `Ctrl+↑`/`↓` swap the entry with the row
next to it, so an entry moved past a line takes it to its other side.

The whole of it was driven through a real `Application` with the
`render-tui-screen` probe: `Ins`, a label, a hotkey and a command typed and
`Enter`, which added the entry under the cursor and grew the popup; a hotkey
another entry holds, refused in place; `Del` on an entry with a line under it;
and `Ctrl+↓` past a line.

```
╔╡Add to the menu╞═══════════════════════════════════════════════╗
║ Label:                                                         ║
║ C                                                              ║
║                                                                ║
║ Hotkey:  c                                                     ║
║                                                                ║
║ Command line:                                                  ║
║ x                                                              ║
║                                                                ║
║ Runs:                          Adds:                           ║
║ ◉ Directly                     ◉ An entry                      ║
║ ○ Shell, window closes         ○ A line above                  ║
║ ○ Shell, window stays          ○ A line below                  ║
║                                                                ║
║ · As administrator                                             ║
║ 'C' already runs 'Compare the panels'.                         ║
║                                                                ║
║                      [ Cancel ] [► Ok ◄]                       ║
╚════════════════════════════════════════════════════════════════╝

╔╡Remove from the menu╞══════════════════════════════════════════╗
║Remove 'Compare the panels' from the menu, or a line next to it?║
║                                                                ║
║          [ Remove entry ] [ Line below ] [► Cancel ◄]          ║
╚════════════════════════════════════════════════════════════════╝
```

The tests: `MenuEntryFormTests` every refusal and the fields of an entry;
`MenuRemovalTests` what is offered beside no line, between two, on the first
row and on a line, the change for each choice and both questions;
`MenuEntryDialogTests` an entry read, the way of running and the rights, a held
hotkey and empty fields refused with the popup open, a line chosen without
fields, the fields greyed, an entry being changed filled in without the choice
of a line and keeping its own hotkey, and a cancel; `UserMenuDialogTests` each
key reaching `Change` and the popup — an entry added after the cursor, into an
empty menu, a line above and below, a change, a removal and its line, a move
each way across a line, a declined question and a change the file refused;
`ShellTests` a change written at once and a refused one reported; and
`UserMenuKeyTests` a menu edited in one window and opened in a second built
over the same file system, which is the restart.
