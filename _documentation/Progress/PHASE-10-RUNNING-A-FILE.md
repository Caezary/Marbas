# Phase 10 — Running a file

The tasks are in [Phase 10 — Running a file](../Plan/PHASE-10-RUNNING-A-FILE.md).

**P10.1 gave the association table a role, which is what phase 10 will run
programs from.** `F4` kept a table of extension → editor and `Enter` will need
one of extension → program; they are now one table with an `AssociationRole` —
`ForEditing` or `ForOpening` — on every row. `EditorPath` became `ProgramPath`
because "editor" had started to lie about half of its uses, and with it
`EditorSettings` became `ProgramSettings` and `AppState.Editors` became
`Programs`; `associate`, `forget`, `associated` and the lookup all take the role
first. Only editing falls back to the default editor: a file no `ForOpening` row
names is one the system should be handed, so the lookup answers nothing rather
than guessing. The settings file spells the role in its keys —
`program.editing.fs=…`, `program.opening.html=…` — and still reads the
role-less `editor.extension.<ext>` a version before this wrote, as a `ForEditing`
row, so an existing config survives the change. The `F12` popup grew a `Used
for` choice and a second list, `Opened with:` beneath `Edited with:`, and `F4`
behaves exactly as it did.

**P10.2 built the way out to a program, and it never waits.** `LaunchRequest`
says what to start, with which program or none for the system's own choice, in
which directory and with which rights (`AsUser` or `AsAdministrator`);
`Dependencies.Launch` is the port, `RunProgram` the effect and `LaunchFinished`
the answer, and `LaunchError` names the four ways it fails as values instead of
one string — nothing associated, the named program missing, the system refusing,
and an elevation prompt the user turned down, which `Update` answers with
silence rather than a dialog. `Adapters/LaunchProcess.fs` is the only place that
knows how: on Windows everything goes through the shell verb, so a console
program gets a console of its own and `runas` is one field more; on Unix an
executable entry is started directly with its streams drained into nothing —
the terminal belongs to the panels — and anything else goes to `xdg-open`.
The same task gave both starters the working directory they should always have
had, the directory the panel stands in.

**P10.3 gave `Enter` the meaning it was missing, and gave the archive walk a key
of its own.** `Enter` now asks four questions in order: a file inside an archive
refuses (`ArchiveEntryNotRunnable`, the way `F4` refuses to edit one); a file on
disk whose extension carries a `ForOpening` row runs that program; a file with
no row that is a readable archive opens as a location, as it always did; and
anything else — an executable, a document, a shortcut — is handed to the system,
which follows a `.lnk` rather than making Marbas read one. Everything that is
not a file on disk falls through to the navigation that was there before, so a
directory and `..` behave exactly as they did. Because an association now beats
the archive walk, `Alt+W` — `Ctrl+PgDn` when P10.3 wrote it — carries that walk
on its own: it is the old meaning of `Enter`, unconditional. The marks are ignored — the cursor entry is
the one that runs. `Executability` grew from five extensions to twelve, Windows'
own `PATHEXT` plus `msi`, `scr` and `cpl`, and learned to answer from the mode
bit alone, which is what the Unix launcher needs.

**P10.4 asked both systems which programs they already know.**
`Dependencies.HandlersFor` takes a name and answers a list of `Handler` — a
display name and the `ProgramPath` behind it — or an empty list, never an error.
Windows reads the registry: the user's choice, the extension's default ProgId,
the `OpenWithProgids` under both roots and the per-user `OpenWithList`, each
resolved through its `shell\open\command` and named by the executable's own file
description where it carries one. Unix reads the XDG mime database: `mime/globs`
turns the extension into a MIME type, `mimeapps.list` turns that into `.desktop`
identifiers, and the `.desktop` files say what each is called and what it runs.
The reading is platform code in `Adapters`; the parsing is pure and sits in
`FileSystem`, so `ProgramCommand`, `DesktopEntry` and `MimeDatabase` are covered
by tests over their own text and the whole Unix side is covered end to end over
a `MockFileSystem`. The Windows side was proved by hand against this machine —
`.html` listed Firefox, Internet Explorer, Edge, Chrome and VS Code, and a name
with no extension listed nothing. `Microsoft.Win32.Registry` turned out to need
no package: the types are in the shared framework on a plain `net10.0` target.

**P10.5 put that list in front of the user on `Alt+R`.** On a file the
popup shows what the system knows — the display name in a column, the path after
it — over a field holding the answer, so a program the list does not offer can
be typed instead. The field opens on the `ForOpening` row the extension already
has, and on the first handler when there is none, and moving over a row writes
its program into it. A box says "Always open `.html` this way": ticked, the
choice becomes a `ForOpening` row in the same table `F12` edits, written to the
settings file on quit, so the answer to "open with" is the answer to the next
`Enter`. It is disabled for a name with no extension, which has no key to file
the row under. On a directory nothing is asked at all — the directory is handed
to the system, which is Explorer on Windows and the desktop's own manager on
Unix — and `..` is the directory the panel stands in rather than the one above
it, because a cursor resting on the way out is not a request to reveal somewhere
else. A panel standing inside an archive
refuses, whatever the cursor is on, because a level of an archive is not a place
the desktop can be sent to. The plan asked for `Shift+Enter`, and that is bound
too, but Windows Terminal drops the modifier and hands the panel a bare `Enter`,
which *runs* the entry instead — so `Alt+R` was added beside it, the way `Alt+H`
sits beside `Ctrl+H`, and it is the binding the documentation names.

**P10.6 raised the rights of an executable on `Ctrl+Alt+Enter`.** It is the run
`Enter` makes, with `AsAdministrator` instead of `AsUser`, which on Windows is
the `runas` verb the shell already knows — and it is the entry's own rights that
are raised, so the association table is not read at all: a `ForOpening` row on
`.exe` changes what `Enter` starts and not what this key does. Only an
executable takes it. A document has no rights of its own to raise and is told
so, `NotAProgram` naming it beside the one sentence that says what elevation is
for; a directory is told the same; `..` says nothing, because there is nothing
there to run. A panel standing inside an archive refuses the way the open-with
key refuses, whatever the cursor is on. A prompt the user turns down was already
silence rather than a dialog — `ElevationDeclined` is the one `LaunchError` that
becomes no effect at all — and on Unix the request never reaches a process:
`LaunchProcess` refuses it by platform and names the reason, because there is no
way to ask for administrator rights that does not put a password prompt on the
screen the panels own. The plan's `Ctrl+Alt+Enter` was the binding it arrived
with; P10.7 found that no terminal delivers it and gave the command a letter.

**P10.7 closed the phase by answering what the terminals actually deliver.**
Two of the three keys the plan named turned out to be fiction. `Shift+Enter`
was already known to arrive as a bare `Enter`, which *runs* the entry;
`Ctrl+Alt+Enter` was tried by hand and arrives as nothing at all, `Alt+Enter`
being the full-screen toggle of the Windows terminals. `Ctrl+PgDn` does arrive.
So each of the three commands now carries two rows in the one `KeyMap` table,
the way `Alt+H` and `Ctrl+H` have always shared `HiddenToggled`: `Alt+W` beside
`Ctrl+PgDn` for the walk, `Alt+R` beside `Shift+Enter` for the open-with popup,
and `Alt+Shift+R` beside `Ctrl+Alt+Enter` for the elevated run. The letters are
what the documentation names and what the tests press; `Alt` with a letter is
the one shape of modified key every terminal in the checklist has been seen to
deliver, and the elevated run is deliberately the open-with key with one more
modifier — one key to remember rather than two. What each terminal really does
with each of them is a row of its own in
[Cross-platform](../Architecture/CROSS-PLATFORM.md), and the one doubt a letter
raises — a driver that reports a shifted letter without its shift flag would
turn `Alt+Shift+R` into `Alt+R` and open the popup instead — was settled by
asking Terminal.Gui's own key converters, both of which keep the flag.

**The same task taught `Operations.mapException` the two Windows codes that had
been reaching it as sentences the system wrote.** 1155, no application
associated, becomes `NoProgramAssociated`; 1223, a prompt the user turned down,
becomes `StartDeclined` — and `EditFinished` answers that one with silence, the
way `LaunchFinished` has always answered `ElevationDeclined`, so a refused
elevation is an answer rather than a dialog whichever key started the program.
`LaunchProcess` has read both codes since P10.2 for the launcher's own errors;
this is the same reading for the editor start, which is the other place a
program is started from and the one that goes through `Operations`.
