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
