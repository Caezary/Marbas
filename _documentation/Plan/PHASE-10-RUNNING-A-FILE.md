# Phase 10 — Running a file

Goal: the entry under the cursor does what it would do outside the manager.
`Enter` runs it, a program of one's own can be named for a whole extension the
way `F4`'s editor already can, and the modifiers reach the two things the
system offers beside a plain run: a choice of program and a run with
administrator rights. Nothing here waits for what it starts — the program is
handed to the system and Marbas goes back to listing directories.

- **P10.1 One association table with a role** — `F4` already keeps a table of
  extension → editor, and `Enter` needs one of extension → program. They become
  the same table with a role on each row (`ForEditing`, `ForOpening`):
  `EditorPath` is renamed `ProgramPath` because "editor" now lies about half of
  its uses, `associate`, `forget` and the lookup all take a role, and the
  settings file spells the role in its keys while reading a role-less line
  written by an earlier version as `ForEditing`, so an existing config survives
  the change. The `F12` popup grows a second section for the opening rows.
  *Done when:* `F4` behaves exactly as before, a config written before this task
  still loads, and the suite says both.
- **P10.2 Starting a program** — `LaunchRequest` (the target, the program to use
  or none for the system's own choice, the working directory, and an
  `Elevation` of `AsUser` or `AsAdministrator`) and a `Launch` port on
  `Shell.Dependencies` answering `Result<unit, LaunchError>`, with
  `LaunchError` covering the four ways it fails as values: nothing is
  associated with the name, the named program does not exist, the system
  refused it, and the user turned down the elevation prompt. The adapter never
  waits: on Windows everything goes through the shell verb, so a console
  program gets a console of its own and the panel is never scribbled over; on
  Unix an executable entry is started directly with its output detached — the
  terminal belongs to the panels — and anything else goes to the desktop's own
  opener. The working directory is the panel's directory, which
  `Adapters/EditorProcess.fs` should have been setting all along and now does.
  *Done when:* the port is exercised through a fake in the suite, and the
  adapter starts a program without the loop pausing for it.
- **P10.3 `Enter` runs the entry** — the key keeps every meaning it has and
  gains one, in this order: `..` and directories navigate as before; a file
  whose extension carries a `ForOpening` row runs that program, which is why
  `Ctrl+PgDn` is bound to the walk into an archive that `Enter` would otherwise
  always do; a file with no row of its own that is a readable archive opens as
  a location, as it does today; anything else is handed to the system, which
  covers both an executable and a document. Marks are ignored — the cursor
  entry is the one that runs — and a file inside an archive refuses with a
  message the way `F4` does, because running a copy from the workspace would
  promise a round trip this phase does not make. `Executability` learns the
  extensions it is missing, and a shortcut is followed by the system rather
  than read. *Done when:* `Enter` runs a document, an executable and an
  associated file, still walks into directories and archives, and says why it
  will do none of it inside one.
- **P10.4 The programs the system knows** — `Handler` (a display name and the
  `ProgramPath` behind it) and a port answering the handlers a name has. The
  Windows adapter reads them from the registry — the ProgIds registered for the
  extension and the per-user open-with list, each resolved through its
  `shell\open\command`; the Unix adapter asks the desktop's mime database,
  reading `mimeapps.list` and the `.desktop` files the XDG directories hold.
  A platform that answers nothing answers an empty list, not an error.
  *Done when:* both adapters list the handlers of a common extension, and the
  parsing either side does is covered by tests over its own text.
- **P10.5 `Shift+Enter` opens with** — on a file, a popup listing what P10.4
  found, with room to type a path of one's own and a box that remembers the
  choice as a `ForOpening` row, so the answer to "open with" can become the
  answer to the next `Enter`. On a directory it opens the system's own file
  manager there, and on `..` the current directory. A panel standing inside an
  archive refuses. *Done when:* a file opens in a program picked from the list,
  the remembered choice survives a restart, and a directory reaches the
  desktop.
- **P10.6 `Ctrl+Alt+Enter` runs elevated** — the same run as `Enter` with
  `AsAdministrator`, which on Windows is the `runas` verb: a prompt the user
  turns down is silence, not an error dialog, because refusing is an answer and
  not a failure. Only an executable entry takes it — a document has no rights
  of its own to raise, and says so. Unix has no equivalent a terminal
  application can ask for without a tty prompt on a screen the panels own, so
  it refuses by platform and names the reason. *Done when:* an executable
  starts elevated on Windows, a cancelled prompt leaves no trace, and Unix
  explains itself.
- **P10.7 The keys and the platforms** — `Enter` with a modifier is the least
  portable thing this phase asks for: `Alt+Enter` is taken by the Windows
  terminals for their own full screen, and outside the kitty keyboard protocol
  a Unix terminal drops the modifier and sends a bare carriage return. Each of
  the three commands therefore gets a second binding that no terminal argues
  about, both are in the one table `KeyMap` already holds, and what a terminal
  really delivers becomes a row of its own in
  [Cross-platform](../Architecture/CROSS-PLATFORM.md). The failures of P10.2 get
  their wording, and `Operations.mapException` learns the two Windows codes
  that reach it as `Win32Exception` — no application associated, and a prompt
  the user turned down. *Done when:* every command is reachable on both
  platforms, and no way of failing to start a program reaches the user as a
  stack trace.

## Libraries

Reading the Windows open-with lists wants `Microsoft.Win32.Registry` (MIT),
because the project builds on one framework for both platforms rather than a
Windows-flavoured one. It is fully managed and carries no native asset, so the
single-file publish of P8.3 stays as it is. Nothing else is needed: the shell
verbs, the desktop opener and the elevation prompt all come from what
`System.Diagnostics.Process` already reaches.
