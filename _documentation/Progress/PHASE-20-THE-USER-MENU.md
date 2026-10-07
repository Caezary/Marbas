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
