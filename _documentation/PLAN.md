# Marbas — Implementation Plan

The plan is split into phases. Each phase produces a runnable, observable
improvement, and each task inside a phase is small enough to be implemented
and verified in one sitting. Tasks are numbered `P<phase>.<task>` so they can
be referenced in commits (e.g. `P2.3: sort directories first`).

## Guiding principles

- **Always runnable** — after every task the app builds and starts.
- **Logic separated from UI** — file-system logic lives in plain F# modules
  (pure functions where possible) so it can be unit-tested without a terminal.
  UI code only wires that logic to Terminal.Gui views.
- **Keyboard first** — every feature must be reachable without a mouse,
  with the classic orthodox key bindings as the reference.
- **Test first** — wherever feasible, implementation starts with a failing
  test (unit tests over the UI-free modules, in-memory file system via
  `System.IO.Abstractions`). Tests are the standing proof the app works.

## Phase 1 — Application skeleton

Goal: an empty but working full-screen TUI application.

- **P1.1 Bootstrap Terminal.Gui** — `Application.Init`, a top-level window,
  clean shutdown. *Done when:* app starts full-screen and exits with `Ctrl+Q`.
- **P1.2 Two-panel layout** — two framed sub-views split 50/50 horizontally,
  resizing with the terminal. *Done when:* both panels are visible and resize
  correctly.
- **P1.3 Panel focus switching** — `Tab` moves focus between panels; the
  focused panel is visually distinct. *Done when:* focus toggles with `Tab`.
- **P1.4 Function-key bar** — bottom bar listing key actions
  (`F5 Copy … F8 Delete F10 Quit`), initially inert except `F10` = quit.
  *Done when:* the bar renders and `F10` exits.

## Phase 2 — Directory listing (read-only)

Goal: panels show real directory contents.

- **P2.1 Directory reading module** — `DirectoryListing` module returning
  entries (name, kind, size, modified date) for a given path; errors returned
  as values, not exceptions. *Done when:* unit-testable function exists.
- **P2.2 Render entries in a panel** — hook the listing into each panel's
  list view; panel title shows the current path. *Done when:* both panels
  list the startup directory.
- **P2.3 Ordering and the ".." entry** — directories first, then files,
  alphabetically; ".." at the top for non-root directories. *Done when:*
  ordering matches the orthodox conventions.
- **P2.4 Entry formatting** — columns for name, size (dirs show `<DIR>`),
  and modification date. *Done when:* columns align and truncate gracefully.

## Phase 3 — Navigation

Goal: independent browsing in each panel.

- **P3.1 Panel state** — each panel tracks its own current directory;
  a shared app-state record holds both panels plus which one is active.
  *Done when:* state model exists and drives rendering.
- **P3.2 Enter a directory** — `Enter` on a directory (or "..") navigates
  into it and refreshes the listing; cursor lands on the directory you came
  from when going up. *Done when:* full up/down navigation works in both
  panels independently.
- **P3.3 Error handling** — inaccessible directories (permissions, removed
  media) show a message box and leave the panel where it was. *Done when:*
  no navigation action can crash the app.
- **P3.4 Manual refresh** — `Ctrl+R` re-reads the active panel. *Done when:*
  external changes appear after refresh.
- **P3.5 Drive / root selection (Windows)** — a dialog (`Alt+F1`/`Alt+F2`)
  to switch a panel to another drive; on Unix, jump to `/` and `~`.
  *Done when:* panels can reach any drive.

## Phase 4 — Selection

Goal: mark files the orthodox way, as input for file operations.

- **P4.1 Single selection cursor info** — status line shows the entry under
  the cursor (full name, size, date). *Done when:* info updates as the cursor
  moves.
- **P4.2 Multi-selection** — `Insert`/`Space` toggles selection and advances
  the cursor; selected entries are highlighted; ".." is never selectable.
  *Done when:* multiple entries can be marked per panel.
- **P4.3 Selection summary & patterns** — count and total size of selected
  files shown in the panel; `+`/`-` (gray keys) select/deselect by wildcard
  pattern. *Done when:* `*.fs` can be selected in one step.

## Phase 5 — File operations

Goal: the manager actually manages files. Every operation acts on the
selection (or the cursor entry when nothing is selected), targets the
opposite panel where applicable, asks for confirmation, and refreshes both
panels afterwards.

- **P5.1 Operations module** — copy/move/rename/delete/mkdir implemented
  in a UI-free module over `System.IO.Abstractions`, returning per-item
  results. *Done when:* covered by unit tests against an in-memory FS.
- **P5.2 F7 make directory** — prompt for a name, create in the active
  panel. *Done when:* new directory appears and gets the cursor.
- **P5.3 F8 delete** — confirmation dialog (extra warning for non-empty
  directories), recursive delete. *Done when:* delete works with errors
  reported per item.
- **P5.4 F5 copy** — copy selection to the other panel's directory, with
  overwrite confirmation (Yes / No / All / Cancel). *Done when:* files and
  directory trees copy correctly.
- **P5.5 F6 move / rename** — move selection to the other panel; when the
  target edit-box is given a bare new name, it acts as rename. *Done when:*
  move and rename both work.
- **P5.6 Progress & cancellation** — long operations run off the UI thread
  with a progress dialog and `Esc` cancellation. *Done when:* copying a
  large tree shows progress and can be cancelled safely.

## Phase 6 — Viewing files

Goal: quick inspection without leaving the manager.

- **P6.1 F3 view** — read-only viewer for the file under the cursor
  (text, scrollable); binary files fall back to a hex-ish/raw view.
  *Done when:* viewer opens and closes with `F3`/`Esc`.
- **P6.2 F4 edit** — launch the system editor (`$EDITOR` / notepad) for the
  cursor file, suspending the TUI; refresh on return. *Done when:* editing
  round-trips cleanly.

## Phase 7 — Polish & configuration

Goal: comfort features that make it feel like a finished tool.

- **P7.1 Classic color scheme** — classic blue panels, cyan selection;
  respects terminals with limited colors. *Done when:* the app looks like the
  DOS-era two-panel managers it follows.
- **P7.2 Sorting options** — `Ctrl+F3..F6`-style toggles: name / extension /
  date / size, ascending & descending, per panel. *Done when:* sort mode is
  switchable and shown in the panel.
- **P7.3 Hidden files toggle** — show/hide dotfiles and hidden/system
  entries. *Done when:* toggle works on both Windows and Unix conventions.
- **P7.4 Persisted settings** — panel paths, sort modes, and toggles saved
  to a config file and restored on start. *Done when:* restart preserves the
  session.
- **P7.5 Modifier-aware key bar** — one table of every key binding, from which
  both the bottom bar and the key handler are built; the bar keeps a slot for
  every function key from `F1` to `F12`, evenly spread and with the key name in
  a background of its own, and holding a modifier swaps the commands on those
  slots for the ones that modifier reaches (`Ctrl` the sort keys, `Alt` the
  drive choosers). *Done when:* the bar follows the modifiers actually held and
  the key names stay put while it does.

## Phase 8 — Quality & release

- **P8.1 Test suite consolidation** — unit tests for listing, sorting,
  selection and operations modules run in CI-friendly fashion
  (`dotnet test`). *Done when:* green test run covers core logic.
- **P8.2 Cross-platform verification** — manual checklist on Windows and
  Linux (paths, drives, permissions, terminal quirks). *Done when:*
  checklist documented in `_documentation/` and passing.
- **P8.3 Packaging** — publish profiles for self-contained single-file
  builds per OS. *Done when:* `dotnet publish` produces a runnable binary.

## Phase 9 — Archives

Goal: pack and later unpack archives without leaving the manager. Creating an
archive comes first; the format vocabulary is chosen so that reading can be
added without reshaping it.

- **P9.1 Archive format and suggested name** — `ArchiveFormat` (`Zip`,
  `TarGz`) with the extension each one owns, and `ArchiveName.suggest`: one
  target gives its own name without the old extension, several targets give
  the active directory's name, a directory root gives `disk<letter>contents`.
  Changing the format swaps the extension on a name already typed.
  *Done when:* suggestion and extension rules are covered by tests.
- **P9.2 Writing an archive** — `Archive.write` walks the sources through
  `IFileSystem` and streams every file into the archive, entry paths relative
  to the source directory and separated by `/`, empty directories kept,
  modification times preserved. It answers an `ItemResult` per top-level
  source the way `copyAll` does, reports each entry through `BatchControl` and
  stops when it is cancelled, deleting the half-written file on the way out.
  *Done when:* archives written on `MockFileSystem` read back with the
  expected entries, and a cancelled run leaves nothing behind.
- **P9.3 The pack dialog** — `Shift+F1` opens a popup with the target (an edit
  box holding the other panel's path and the suggested name, read like `F6`'s),
  the format, the compression level and a password field.
  *Done when:* the dialog answers with everything the effect needs, or with
  nothing when it is cancelled.
- **P9.4 Packing end to end** — `PackRequested` → the dialog →
  `ArchiveTargets` → the batch → `PackFinished`: the operation targets of the
  active panel are packed into the other panel's directory, an existing
  archive asks the overwrite question, a target that would land inside what is
  being packed is refused, the other panel is re-read with the cursor on the
  new archive, and the marks of what did not make it survive.
  *Done when:* `Shift+F1` produces an archive from marks or from the cursor
  entry.
- **P9.5 Password-protected zip** — a password turns the zip into an AES-256
  one; the field is disabled for the formats that cannot carry one.
  *Done when:* a protected archive refuses to open without the password.

Reading an archive comes next, and it reads formats Marbas cannot write — `rar`
above all, whose compression is proprietary and which therefore never appears
among the formats an archive can be created in.

- **P9.6 Archive recognition** — `ArchiveKind`
  recognised from a name (`zip`, `tar`, `tar.gz`, `tar.bz2`, `tar.xz`, `7z`,
  `rar`), compound suffixes read whole so `release.tar.gz` is a tar.gz and not a
  gz, and one capability record per kind: whether Marbas can read it, create it,
  and change what is in it — adding an entry, removing one, making a directory —
  which is zip and, from P9.14, 7z. `ArchiveFormat` — the set an archive can be
  created in — becomes the creatable projection of that set rather than a second
  vocabulary kept in step by hand. *Done when:* recognition and capabilities are
  covered by tests, and the pack dialog offers exactly the kinds that claim they
  can be created.
- **P9.7 Reading an archive** — `ArchiveListing.read` opens the file through
  `IFileSystem` with `SharpCompress` and answers the entries of one level inside
  it as the same `Entry` list a directory gives — sizes, modification times, and
  directories synthesised where the archive stores none — so sorting, hidden
  entries, marks and the search need no new code. A path inside an archive gets
  a domain type of its own (`ArchivePath`: `/`-separated, never rooted, no `..`),
  and failures come back as values (`ArchiveError`: not a kind that can be read,
  damaged, password needed). *Done when:* archives written on `MockFileSystem`
  list back level by level, including empty and merely implied directories.
- **P9.8 Extracting** — `Archive.extract` streams named entries out of an
  archive into a target directory through `IFileSystem`, a directory taking the
  tree below it, restoring modification times and refusing an entry whose path
  would climb out of the target. It answers an `ItemResult` per top-level target
  the way `Archive.write` does, reports each entry through `BatchControl` and
  stops when cancelled, leaving behind what it had already written. *Done when:*
  a round trip through `write` and `extract` on `MockFileSystem` returns the
  tree it started from, and a cancelled run stops where it stood.
- **P9.9 `Shift+F2` unpack** — `UnpackRequested` → `ExtractTargets` → the batch →
  `UnpackFinished`: the archive under the cursor, or every archive marked, is
  unpacked into the other panel's directory, a file already there asks the
  ordinary five-answer overwrite question, a password-protected archive asks for
  the password once, a kind that cannot be read reports itself as an
  `OperationError`, and the other panel is re-read afterwards. The bar gets an
  `Unpack` slot on `Shift+F2`. *Done when:* `Shift+F2` empties an archive into
  the other panel.
- **P9.10 A panel that is not on disk** — `PanelLocation` is either a directory
  on disk or a place inside an archive (the archive's own `FileSystemPath` plus
  an `ArchivePath`); `PanelState.Path` becomes one, the panel title spells it
  `…\backup.zip\docs`, and every operation asks the location what it allows
  instead of assuming a disk path. A panel standing inside an archive saves the
  directory *holding* the archive to the settings file, because a session should
  restore to a place that is certainly still there. *Done when:* the type is in
  place, both panels browse the disk exactly as before, and the suite says so.
- **P9.11 Walking into an archive** — `Enter` on a readable archive opens it as
  a location instead of doing nothing, `..` at its top level leaves it with the
  cursor back on the archive file, deeper levels walk in and out, `Ctrl+R`
  re-reads it, and `F3` views a file inside one by streaming that entry into the
  viewer. `F4` is the one key that refuses with a message: editing an entry in
  place would mean rewriting the archive when the editor returns, which is a
  promise this phase does not make. *Done when:* a zip, a tar.gz, a 7z and a rar
  all browse like directories.
- **P9.12 Copying out of an archive** — `F5` from a panel inside an archive
  extracts its operation targets into the other panel's directory, marked
  directories taking their trees, running behind the same progress window with
  the same overwrite question and the same surviving marks as a copy between two
  disk directories. *Done when:* `F5` inside an archive is indistinguishable
  from `F5` between directories, and the archive is left untouched.
- **P9.13 Changing an archive** — the operations that write into a panel
  standing inside an archive, each gated on the kind's capability: `F5` adds the
  other panel's targets at that level, `F8` removes the targets of an archive
  panel, `F7` makes a directory entry, and `F6` moves in either direction —
  out of an archive it extracts first and drops the entry only for what arrived,
  into an archive it deletes a source only once that source is in, so a failure
  loses nothing. A kind whose entries cannot be changed refuses by name before
  anything runs, which for `F6` means moving out of a rar is refused where
  copying out of it works. Zip is the changeable kind here; 7z joins it in P9.14
  by turning one flag in the table, since these operations ask the table rather
  than name a format. *Done when:* a file added in the panel is in the archive
  on disk after the panel re-reads, a removed one is gone from it, a moved one
  is in exactly one of the two places, and a tar.gz says why it will do none
  of it.
- **P9.14 The remaining formats** — 7z joins the set an archive can be created
  in through `SharpCompress` (non-solid LZMA2, and no password, which the
  dialog's field already knows how to disable) and with it the set whose entries
  P9.13 can change, an existing 7z being rewritten whole where a zip is updated
  in place; tar, tar.bz2 and tar.xz join the set that can be read; rar stays
  readable and uncreatable for good. The pack dialog's format list and its
  password field follow the capability table of P9.6 rather than a list kept by
  hand, and a small checked-in rar fixture is what proves the read side. Should
  `SharpCompress` turn out not to write the 7z Marbas needs, 7z stays on the
  reading side and the table says so. *Done when:* every kind does exactly what
  its capabilities claim — a round trip per creatable kind, an add and a remove
  per changeable one, and a listing and an extraction per readable one.

### Libraries

`SharpZipLib` (MIT) writes zip, because it is the only maintained managed
library that can encrypt one, and it is what rewrites a zip when P9.13 adds an
entry or removes one; `System.Formats.Tar` over a `GZipStream` writes tar.gz;
`SharpCompress` (MIT) reads everything — zip, the tar family, 7z and rar — and
from P9.14 writes 7z as well. All three are fully managed, which keeps the
single-file publish of P8.3 intact, and none is copyleft. Wrappers around the
native `7z.dll` are LGPL and were rejected on both counts.

## Phase 10 — Running a file

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
  [Cross-platform](Architecture/CROSS-PLATFORM.md). The failures of P10.2 get
  their wording, and `Operations.mapException` learns the two Windows codes
  that reach it as `Win32Exception` — no application associated, and a prompt
  the user turned down. *Done when:* every command is reachable on both
  platforms, and no way of failing to start a program reaches the user as a
  stack trace.

### Libraries

Reading the Windows open-with lists wants `Microsoft.Win32.Registry` (MIT),
because the project builds on one framework for both platforms rather than a
Windows-flavoured one. It is fully managed and carries no native asset, so the
single-file publish of P8.3 stays as it is. Nothing else is needed: the shell
verbs, the desktop opener and the elevation prompt all come from what
`System.Diagnostics.Process` already reaches.

## Phase 11 — Tabs

Goal: one Marbas holds several pairs of panels and remembers them between runs.
A tab is the pair — the left panel, the right one, and which of the two is
active — so switching one changes both panels at once. `F11` lists them,
`Alt+F11` opens one on the directories already on screen, `Alt+Shift+F11` closes
the current one, and the last one cannot be closed. The session file grows from
one pair into a numbered list, and every command that opens, switches or closes
a tab writes it, which is also all `Shift+F9` does.

What the terminals really deliver was measured before any of these keys was
bound, the way P10.7 measured `Enter` with a modifier. Five runs of
`_probes/KeyProbe` — cmder, Windows Terminal, cmd, and a WSL session started
from each of the first and the third — agree on everything but two keys. `F11`
never reaches the application under Windows Terminal or under cmd, which both
keep it for full screen, while cmder passes it through; and a WSL session
inherits whatever the terminal hosting it does, so it is the host and not the
platform that decides. `Ctrl+T`, the obvious second binding, is the one cmder
keeps for its own new-console popup. Everything else — `Alt+F11`,
`Alt+Shift+F11`, `Shift+F9`, `Alt+Left`, `Alt+Right`, `Alt+T`, `Alt+Shift+T` and
`Ctrl+F11` — arrives everywhere. The popup therefore gets two bindings, `F11`
because that is what the key bar advertises and `Alt+T` because that is what
works where `F11` is eaten; the other four keys need one each.

- **P11.1 A tab is a value** — `Tab` (the left panel, the right panel, the
  active side) and `Tabs`, a zipper of the tabs before it, the current one and
  the tabs after it, so "there is always exactly one current tab" and "the last
  tab cannot be closed" are facts of the type rather than checks at runtime.
  `AppState` keeps `LastCreatedDirectory` and `Programs`, which belong to the
  session and not to a place, and hands the panels to the current tab: `panel`,
  `updatePanel` and a new `activeSide` are the funnel every transition already
  goes through, so `state.Active` becomes `activeSide state` and nothing else in
  `Update` moves. `openTab` clones the current tab without its marks — a mark is
  an operation in progress, not a property of a place — and `switchTab` and
  `closeTab` complete the set. *Done when:* every existing test passes
  unchanged, and new ones cover opening, switching, and the close the only tab
  refuses.
- **P11.2 Tabs in the session file** — `Settings` carries a list of tabs and
  which one is current instead of one pair of panels: `tab.1.left.path`,
  `tab.1.left.sort`, `tab.1.left.direction`, `tab.1.left.hidden`, the same four
  for the right, `tab.1.active`, and one `tabs.current`. A file with no `tab.`
  key is read as a single tab from the `left.`/`right.` keys an earlier version
  wrote — the way P10.1 reads a role-less editor line — and the first save
  rewrites the file in the new spelling, so there is nothing to migrate. A panel
  standing inside an archive is written as the directory holding it, as it has
  been since P9.10, and a stored path that is no longer a directory falls back
  per panel, so one removed drive costs one panel and not the file. *Done when:*
  a config written before this task loads as one tab, three tabs survive a
  restart, and a hand-edited file still opens.
- **P11.3 Opening, closing and saving** — `TabOpened` on `Alt+F11` and
  `TabClosed` on `Alt+Shift+F11`, each answering with the new state and
  `SaveSettings`, and `Shift+F9` for the save on its own, which writes without a
  dialog because a save that succeeded has nothing to say. Closing the only tab
  is not a failure to report but a key that does nothing: the type cannot even
  express the state it would produce. *Done when:* a new tab opens on the
  directories already on screen, the file on disk changes after each of the
  three keys, and the last tab stays.
- **P11.4 Switching** — `F11` and `Alt+T` raise `ShowTabs`, a `ChooseTab` port answers with
  the tab that was picked or with nothing, and `TabSwitched` moves the zipper and
  saves. `TabDialog` lists one row per tab — its number, its left location and
  its right one — opening on the current one, `Enter` choosing and `Esc`
  changing nothing. `Alt+Left` and `Alt+Right` cycle without the popup;
  `Ctrl+Tab` is spoken for by the terminals. Switching reloads both panels of
  the tab it lands on, since a tab that has been sitting in the background may
  be standing in a directory that has since gone, and startup therefore loads
  only the current tab and the others when they are first reached. *Done when:*
  a tab reached from the popup or with `Alt+Right` shows the directories as they
  are now, and the session is written.
- **P11.5 The counter and the shortened title** — how many tabs are open sits in
  the top right corner as `[2/3]`, which is the top border of the right panel and
  therefore the line its title is written on, so the title gives way:
  `PanelTitle.describe` takes the width it may use and shortens a path that will
  not fit to `C:\...\parent\current`, keeping the root, the directory the panel
  stands in and the one holding it, because those are what tell one panel from
  another. Read off a rendered screen, not off a property of the view. *Done
  when:* the counter is legible with one tab and with several, a long path is
  shortened rather than clipped, and the sort marks keep their place.
- **P11.6 Cursors in the file** — `tab.1.left.cursor=<name>` and its right-hand
  twin, resolved on load the way `FocusTarget.ByName` already resolves a focus
  request and simply not written for an empty panel. A name that is no longer
  there leaves the cursor at the top rather than refusing the tab. Marks are
  still not saved: they belong to an operation, and an operation does not
  survive a restart. *Done when:* every tab comes back with both its cursors
  where they were, and a renamed entry costs nothing but the position.
- **P11.7 The keys and the platforms** — what the probe found becomes a row of
  its own in [Cross-platform](Architecture/CROSS-PLATFORM.md): which terminal
  keeps `F11`, which keeps `Ctrl+T`, and that a WSL session inherits the
  terminal it was started from rather than answering for Linux. The second
  binding it argues for is already in the one table `KeyMap` holds, so what is
  left is the measurement repeated against the finished keys. *Done when:*
  opening, closing and listing tabs are each reachable in cmder, in Windows
  Terminal and in cmd, and under a WSL session started from any of them.
- **P11.8 The documentation** — `Architecture/TABS.md` says what a tab is, why
  the zipper, what a new one inherits and what becomes of a panel standing
  inside an archive; [Settings persistence](Architecture/SETTINGS-PERSISTENCE.md)
  and [State and effects](Architecture/STATE-AND-EFFECTS.md) gain what the file
  and the transitions grew, and the user guide gains the four keys. *Done when:*
  the phase is described where the rest of the design is, not only in this plan.

## Phase 12 — Watching the directories on screen

Goal: what the panels show is what the disk holds. A file written by another
program, a directory removed from a shell, a renamed archive, the whole
directory a panel stands in deleted, a memory stick pulled out — each of them
reaches the screen on its own, without `Ctrl+R`. Only the tab in front is
watched: a tab in the background is already re-read when it is switched to
(P11.4), so a watch on it would pay for a listing nobody is looking at. The
system tells us most of this through `FileSystemWatcher`, which
`System.IO.Abstractions` hands over as `fs.FileSystemWatcher.New`; the rest —
the watched directory dying under the watcher, and the drive under the
directory — it does not say, and a poll has to ask.

- **P12.1 The re-read and the message that asks for it** — `ExternalChangeDetected`
  answers with both panels of the tab in front read again, each cursor kept on
  the entry it stood on and the marks that survived, which is `readTabAgain`
  already. It differs from `RefreshRequested` in two ways, and that is why it is
  a message of its own rather than a second sender for that one: `Ctrl+R`
  refreshes the active panel and this refreshes the pair, and a failed listing
  here raises no dialog, because a change the user did not ask for must not
  answer with a popup. A listing that fails leaves the panel as it was and waits
  for P12.4 to place it. *Done when:* a directory changed underneath a
  `MockFileSystem` shows its new entries after the message, with the cursor and
  the surviving marks where they were, and a vanished directory produces no
  effect.
- **P12.2 The locations under watch** — `WatchLocations` is an effect carrying
  the paths the tab in front stands on, and the update raises it wherever that
  pair can change: at startup, on navigation into and out of a directory, on a
  root, on a path opened, and on a tab opened, closed, switched or cycled. A
  panel standing inside an archive contributes the archive file, not the level
  inside it, since the file is what the operating system can report on. Both
  panels in one directory contribute it once. *Done when:* every transition that
  moves a panel is proved to answer with the effect, and the paths in it are the
  ones on screen.
- **P12.3 The adapter and the burst** — `Adapters/DirectoryWatch.fs` holds the
  only `FileSystemWatcher` in the app: one per path, `IncludeSubdirectories`
  off, `NotifyFilters.FileName ||| DirectoryName ||| LastWrite ||| Size |||
  Attributes`, because a panel shows a size and a date as well as a name. A new
  `WatchLocations` disposes the watchers of the previous pair before it creates
  the next. Its events arrive on a thread-pool thread and reach the loop through
  `app.Invoke`, as the batch runner's progress already does; one copy raises
  hundreds of them, so the first arming a timeout and the rest only pushing it
  along collapses a burst into a single re-read. *Done when:* a file created,
  deleted or renamed by another process appears or goes within the debounce
  interval, and a directory copied into a watched one costs one listing rather
  than one per file.
- **P12.4 What the watcher cannot say** — three things it does not report, all
  arriving as `Error` or as nothing at all: the watched directory deleted
  underneath it, the buffer overflowing under heavy traffic, and the drive
  removed, which no console application can hear, since the device notification
  the system offers wants a window handle. `Error` is therefore not an error to
  show but a signal to read again, and a poll on a timeout is the net beneath
  it, asking only whether the paths on screen still exist. A panel whose
  location is gone walks up to the nearest ancestor that is still there rather
  than reporting the loss, because it stands somewhere now and the user did
  nothing to move it. *Done when:* deleting the directory a panel stands in from
  a shell leaves that panel in its parent, and pulling out the drive a panel
  stands on leaves it on a root that exists, with no dialog either time.
- **P12.5 Quiet while something is in front** — Terminal.Gui runs a dialog in a
  nested loop, so a timeout keeps firing while a prompt is open, and a re-read
  underneath one would move the cursor the prompt is about to act on. Delivery
  is therefore held while a modal is up and while a batch runs, and what was
  held becomes one re-read when the screen is the panels' own again. The
  operations that already finish with a listing of their own keep it; a held
  change is not a second one. *Done when:* a change made while `F5` is asking
  for its target does not move the entry under the cursor, and the panels show
  it once the dialog closes.
- **P12.6 The platforms and the documentation** — `Architecture/AUTO-REFRESH.md`
  says what is watched, what is not, why the errors are read as a request to
  look again, and why the tabs in the background are left alone;
  [Cross-platform](Architecture/CROSS-PLATFORM.md) gains what the two systems
  do differently — `ReadDirectoryChangesW` against `inotify`, the per-user watch
  limit on Linux, and the network shares and file systems that report nothing,
  which is the other reason the poll of P12.4 exists. *Done when:* the phase is
  described where the rest of the design is, and the manual test is repeated on
  both systems.

## Phase 13 — The drive, the link and what is left behind

Goal: five commands about what an entry sits on and what becomes of it, rather
than about the entry itself. `Alt+F1` and `Alt+F2` stop offering a bare list of
paths and show the drives as drives — what kind each one is, how large it is and
how much of it is free — landing the panel where the other panel already stands
when the drive chosen is the drive that one is on. `Ctrl+\` reaches the root of
the volume in one key. `F3`, which does nothing at all on a directory today,
measures it. `Alt+F6` puts a link beside what it points at, in all three kinds
the two systems know. `Alt+Del` removes an entry without leaving its contents
behind on the disk.

What the terminals deliver was measured before any of these keys was bound, the
way P10.7 measured `Enter` with a modifier and P11 measured `F11`. Five runs of
`_probes/KeyProbe` on 2026-09-12 — cmder, Windows Terminal, the console, and a
WSL session started from the first and from the third — agree on every one of
them. `Alt+Del` arrives everywhere and wants no stand-in. `Ctrl+\` never arrives
as a backslash: the terminal sends the control byte `0x1C` and the driver reads
that byte back as `Ctrl+4`, the other key it belongs to, so `Ctrl+4` is what the
command is bound to and `Ctrl+\` is kept beside it. `Alt+\`, the way out this
phase had planned, arrives as nothing at all. And `Shift+Enter` inside a popup is
the bare `Enter` P10.7 already found in the panel, which on a popup is the
default button — so `Alt+R`, which does arrive inside a modal dialog, is the only
key P13.15 takes.

- **P13.1 The keys the terminals deliver** — `_probes/KeyProbe` gains `Alt+Del`,
  `Ctrl+\` and the stand-ins the phase would fall back on, `Alt+\` and
  `Alt+Shift+Delete`, and asks besides whether `Shift+Enter` and `Alt+R` reach a
  dialog that is up rather than the panel behind it, since P13.15 wants them
  there. Five runs as in P11 — cmder, Windows Terminal, cmd, and a WSL session
  started from the first and from the third. *Done when:* each new key either
  arrives everywhere or has a second binding that does, and what was measured is
  a row in [Cross-platform](Architecture/CROSS-PLATFORM.md).
- **P13.2 The root of the volume** — `VolumeRootRequested` walks the active
  panel to the root of the path it stands on, on `Ctrl+4` — which is what
  pressing `Ctrl+\` delivers — with `Ctrl+\` itself bound beside it, the way
  `hiddenKeys` holds two. A panel inside an archive leaves
  the archive, because the root asked for is the volume's and the archive is a
  file on it; a panel already standing on a root has nothing to do and says so
  by staying. The move goes through the transition `PathOpened` already uses, so
  the cursor rules and the watch of P12.2 hold without a second path through the
  update. *Done when:* a panel nested three directories deep, one on the root,
  one inside an archive and one on `/` are each covered, and the effect the
  watch needs is raised.
- **P13.3 Measuring a directory** — `DirectoryMeasure.measure` walks everything
  below a directory through `IFileSystem`, summing the lengths of the files,
  reporting each directory it enters through the `BatchControl` the batches
  already carry and stopping when that control is cancelled. A subdirectory it
  cannot read is skipped rather than raised, which makes the answer a partial
  one, so the answer is a value that says which it is — an exact total or a
  lower bound. A directory link is a leaf here as everywhere else (P13.8), so a
  link pointing at its own ancestor cannot spin. A directory inside an archive is
  measured from the archive's index instead: read once through `ArchiveReader`,
  summed below the level asked for, every level under it reported through the same
  control. Level by level through `ArchiveListing` would re-open and re-scan the
  whole archive once per directory, which a packed source tree would pay for a
  few hundred times over. What it answers is what the entries say they hold
  unpacked, not what they cost packed. *Done when:* a nested tree on `MockFileSystem`
  measures to the byte, an unreadable subdirectory answers a lower bound, a level
  inside an archive answers the unpacked size of everything under it, and a
  cancelled walk answers what it had counted.
- **P13.4 `F3` on a directory** — `ViewRequested` on a directory raises
  `MeasureTargets`, over the operation targets every other key takes — the
  marks, or the entry under the cursor — and the walk runs behind the progress
  window with `Esc` stopping it, since a tree of a hundred thousand files must
  not hold the loop. What comes back is kept against the entries of that panel
  and shown in two places: the size column, in place of the `<DIR>` the entry
  wears today, and the status line under the panels, a lower bound prefixed with
  `≥`. A measurement belongs to the listing it was taken over, so re-reading the
  panel — `Ctrl+R`, or the watch of P12 — forgets it. `..` measures the
  directory it leads to: the parent, and with it the panel's own tree and every
  sibling beside it.
  *Done when:* `F3` on a directory shows its size in both places, several marks
  are measured in one run, and `Esc` leaves the panel as it was.
- **P13.5 Sorting the measured directories** — sorting by size compares
  directories by what was measured and files by their length, which the group
  rank of P2 already keeps apart, so the two never meet in one comparison. A
  directory nobody has measured counts as nothing and therefore keeps the name
  order among its own kind, which is what the panel showed before the key was
  pressed. *Done when:* a panel of measured and unmeasured directories and files
  sorts as described in both directions.
- **P13.6 Wiping** — `Operations.wipe` clears the read-only attribute, writes one
  pass of random bytes over the whole length of the file through `IFileSystem`,
  buffer by buffer, reporting and asking the batch control about cancellation at
  each of them, flushes, truncates the file to nothing, renames it to a name of
  its own length in the same directory and then deletes it — the name being as
  much of a leak as the contents. A directory is walked depth-first and removed
  after what was in it. One pass, not three: on a solid-state disk with `TRIM`,
  on a copy-on-write file system and on one with a journal the further passes
  buy nothing that the first did not, and cost the whole length of the file
  again. A run stopped inside a file leaves that file where it is — its contents
  are already gone — and reports it, because pretending `Esc` undid it would be
  untrue. *Done when:* the mock shows the contents replaced before the entry is
  removed, a read-only file is wiped rather than refused, and a cancelled run
  names the file it had begun.
- **P13.7 `Alt+Del`** — `DeleteMode` gains a third case beside `ToTrash` and
  `Permanently`, and the confirmation carries words of its own: what wiping
  does, and what it cannot promise — that on a solid-state disk, on a
  copy-on-write file system or on one with a journal the old blocks may outlive
  the file anyway. The effect leaves the synchronous path the other two deletes
  take and runs through `RunBatch`, which is what the rough edge recorded under
  delete has been asking for: this is the one delete that has something to
  report. The panel is read again afterwards and the marks of what survived
  stay. *Done when:* `Alt+Del` wipes the operation targets behind the progress
  window, the confirmation says what it is about to do, and `Esc` stops the run.
- **P13.8 A link says it is one, and the walks stop at it** — the listing asks
  `ResolveLinkTarget` and an entry carries what it found, so a link is drawn in
  a colour of its own and the status line says `-> target` after its name. Every
  recursive walk in the app — copying, moving across volumes, packing,
  measuring, wiping, deleting — treats a directory link as a leaf: the link is
  taken, the tree under it is not. That is what keeps a link pointing at its own
  ancestor from spinning, and it is the reason this task comes before anything
  creates one. What .NET does on its own here is proved by a test rather than
  assumed. *Done when:* a directory link is copied as a link and not as a tree,
  deleting one leaves what it points at, and a cycle terminates.
- **P13.9 The symbolic link** — `LinkKind` (`Symbolic`, `Hard`, `Junction`) and
  the request that carries a kind, a target and where the link is to stand.
  `Links.create` makes the symbolic one through `fs.File.CreateSymbolicLink` and
  its directory twin, which `MockFileSystem` implements, so the rule that logic
  is tested without touching the disk holds for the kind that matters most. The
  target is written absolute. Windows refuses a symbolic link to anyone who is
  neither an administrator nor in developer mode, which arrives as a bare access
  refusal and must not be reported as one: it is a refusal of its own, saying
  which of the two to switch on. *Done when:* links are made and resolved on the
  mock, and the Windows refusal is mapped rather than passed through.
- **P13.10 The two the framework does not give** — neither a hard link nor a
  junction has a managed API. `CreateHardLinkW` in `kernel32` makes the first on
  Windows and `link` in `libc` makes it on Unix; a junction is a reparse point
  written with `DeviceIoControl` and exists on Windows alone. All three live in
  `Adapters/NativeLinks.fs`, behind a `MakeLink` port in `Shell.Dependencies`,
  so the update and the dialog stay testable against a fake and only `Adapters`
  touches the system, as the rule has it. What a kind can point at is a fact of
  the kind and is answered before the system is asked: a hard link only to a
  file on the same volume, a junction only to a directory and only on Windows, a
  symbolic link to either on both. *Done when:* each refusal is answered without
  a system call, and each kind is made once against the real file system in the
  integration tests.
- **P13.11 `Alt+F6`** — the popup asks for the target — an edit box holding the
  other panel's directory and the name of what is under the cursor, read the way
  `F6` reads its own — and for the kind, with the kinds this target cannot carry
  disabled and the reason on one line beneath. Several marks make one link each
  inside the directory that was typed, the box then naming that directory alone,
  which is how `F5` and `F6` already read a target with several sources. The
  other panel is read again with the cursor on what was made. *Done when:*
  `Alt+F6` makes each of the three kinds where the system allows it, refuses the
  rest with a sentence, and the new link is visible as a link straight away.
- **P13.12 A drive is a value** — `Drive` — the root it is reached by, its
  label, its kind, its size and its free space — and `DriveKind`, which says
  `fixed`, `removable`, `network` or `optical` rather than leaving a
  `System.IO` enumeration to reach the screen. `Drives.list` reads them through
  `fs.DriveInfo` and hides what cannot be used: a drive that is not ready, which
  is what an empty card reader looks like, and on Unix the mounts that are not
  storage at all — `squashfs`, `tmpfs`, `devtmpfs`, `overlay` and their like —
  which otherwise bury the two real mounts under a dozen snap loops. The home
  directory keeps the row `RootAndHome` gives it today. `Roots` and `RootChoice`
  fold into this; the root prompt is their only caller. *Done when:*
  `MockFileSystem` with drives added covers the kinds, the sizes, both filters
  and the home row, on each platform's spelling of a root.
- **P13.13 The drive popup** — one row per drive: the root, the label, the kind
  spelled out, the total size and the free one, in columns that line up the way
  `TabRow` lines up tabs. A `ChooseDrive` port of its own replaces the generic
  `ChooseOption` for this effect, because a row is now four things and not a
  path. The list is read at the moment the popup opens, so a stick plugged in
  while Marbas was running is simply there, and nothing polls for it in the
  background. Read off a rendered screen, not off a property of the view. *Done
  when:* `Alt+F1` and `Alt+F2` show the drives with their sizes, a drive added
  between two openings appears in the second, and `Esc` changes nothing.
- **P13.14 Where the panel lands** — the drive chosen is the drive the other
  panel stands on, and the panel goes to that panel's directory rather than to
  the root: two panels on one disk are almost always wanted in one place. Any
  other drive gives its root, and so does the drive this panel is already on,
  since `Ctrl+\` is the key that asks for a root deliberately. A drive that went
  away between the listing and the choice — the stick pulled out while the popup
  was up — reports itself as the listing failure it is and leaves the panel
  where it was. *Done when:* each of the three cases is covered by a test over
  the update, and the vanished drive raises a dialog rather than an empty panel.
- **P13.15 The drive in the system file manager** — `Alt+R`, one of the two keys
  the panel already uses to hand something to the system, answers inside the
  popup as well, and what it answers with is a second kind of choice: the drive
  to stand on, or the drive to hand over. `Shift+Enter`, its twin in the panel,
  is not bound here — P13.1 found it arriving as a bare `Enter`, which a popup
  reads as its default button, so it would put the panel on the drive the user
  asked to see elsewhere. Handing it over is the `LaunchRequest` of P10.2
  unchanged — on Windows the shell opens the directory in the system file
  manager, on Unix `xdg-open` does — and the popup closes with both panels where
  they were. *Done when:* `Alt+R` on a drive opens it outside Marbas and moves
  neither panel.
- **P13.16 The documentation** — `Architecture/LINKS.md` says what the three
  kinds are, which system carries which, why only one of them is testable
  without a disk, and why every recursive walk stops at a directory link;
  [File operations](Architecture/FILE-OPERATIONS.md) gains wiping — the one
  pass, the name, and what it cannot promise — and the measurement behind `F3`;
  [UI and input](Architecture/UI-AND-INPUT.md) gains the drive popup, the size
  column a measured directory fills and the three new keys;
  [Cross-platform](Architecture/CROSS-PLATFORM.md) gains the interop row and the
  Unix mount filter; and the user guide gains all five commands. *Done when:*
  the phase is described where the rest of the design is, not only in this plan.

### Interop

Nothing new is taken from the package feed. The hard link and the junction are
three `DllImport` declarations — `kernel32` for `CreateHardLinkW` and
`DeviceIoControl`, `libc` for `link` — which the single-file publish of P8.3
carries as it carries the rest of the runtime, since a platform invoke into a
system library bundles nothing. The price is that these two kinds cannot be
exercised on `MockFileSystem`, which is why P13.10 answers every refusal it can
before calling out and leaves the calls themselves to the integration tests.

## Phase 14 — Finding files

Goal: find entries by name and by what they contain, anywhere from the directory
a panel stands in to every drive the machine has, and do something with what was
found without retyping a path. `Alt+F7` asks the question in a popup of ordinary
size; the answer arrives in a second, full-screen window while the search is
still running, grouped by the directory each hit lives in, with a summary line
saying how much was found and where the walk is now. From there `Enter` puts the
active panel on a hit, `F3` views one, and the whole answer can be sent into the
panel as a temporary listing whose `..` leads back to where the panel stood
before the search. The last answer of the session can be reopened, so the walk
from one hit to the next is `Alt+F7`, `↓`, `Enter` rather than a new search each
time.

What the keys really do was measured before anything was bound to them, the way
P10.7, P11 and P13.1 measured theirs, and P14.1 found two things worth carrying
into the tasks below. `Alt+F7` is delivered by every terminal tried, Windows and
VTE alike, and is nevertheless lost on a GNOME desktop, whose window manager
claims `<Alt>F7` for moving a window before the terminal is sent anything — so
the phase binds `Alt+Shift+F` beside it, the letter-with-`Alt` shape that has
travelled everywhere since P10.7. And `Enter` reaches a full-screen `Window`
with no buttons but not a `Dialog`, where it presses the default button before a
handler sees it. The results window is therefore built like the `F3` viewer
rather than like a popup, and everything it offers is reached by a key rather
than by a button.

Two decisions run through the whole phase. The first is where a running search
keeps its hits: in the results window, not in `AppState`. A search over a whole
disk can find a hundred thousand entries, and turning each of them into a message
through `Update` would pay a state transition per hit for a list nobody is
looking at yet; what reaches `Update` is the finished answer — or the stopped one
— once, when the window closes. The second is that this is the one walk in the
app allowed to follow a directory link, and only when asked. P13.8 stopped every
recursive walk at a link because a link to an ancestor spins; a search that
follows links therefore carries a set of the real paths it has entered and refuses
to enter one twice.

- **P14.1 The keys the terminals deliver** — `Alt+F7` is the classic key, and it
  is also what a desktop takes for "move the window" before the terminal sees it.
  The measurement therefore splits in two, which is new for this checklist: what
  a terminal encodes, which a probe can press, and what a window manager claims,
  which is read out of its own configuration and cannot be pressed without a
  desktop. `_probes/KeyProbe` gains `Alt+F7` and the stand-in the phase would fall
  back on, `Alt+Shift+F`, and asks besides whether `F3`, `Enter` and `Esc` reach a
  full-screen window that is up rather than the panel behind it, since the results
  window wants all three. Four runs — cmder, Windows Terminal, the Windows
  console, and `gnome-terminal` under WSLg, which is a real VTE terminal and the
  half of a Linux desktop that can be reached from this machine — and GNOME's
  defaults read with `gsettings` for the other half. *Done when:* the search is
  reachable by a key that arrives everywhere, and what was measured is a row in
  [Cross-platform](Architecture/CROSS-PLATFORM.md).
- **P14.2 The question is a value** — `FindQuery` in `Domain` carries the name
  masks, the text, the scope and the options, and makes the combinations that
  mean nothing impossible to build rather than checked later. `NameMasks` reads
  the typed text split at `;`, each mask trimmed: an empty field and `*.*` both
  mean every name, which is what a user of the classic managers expects and what
  `FileSystemName.MatchesSimpleExpression` does not do on its own (`*.*` there
  wants a dot, and passes `Makefile` over); a mask with no `*` or `?` is a
  fragment, so `report` is `*report*`. Masks are matched against the name alone,
  never against a relative path, and always without regard to case — case is an
  option of the text and only of the text. `FindScope` is one of `FromDirectory`,
  `DirectoryOnly`, `MarkedDirectories`, `FromVolumeRoot`, `AllDrives` and
  `PathDirectories`. The options are the text's case, whole words, not
  containing, binary files, archives, directories (on by default), links (on by
  default) and hidden entries (defaulting to what the panel conceals). A query
  with text finds no directories, since a directory contains no text; "not
  containing" and "binary files" exist only with text. *Done when:* the reading of
  masks and every refused combination are covered by tests, and `NamePattern` —
  what `+` and `-` mark with — is left exactly as it is.
- **P14.3 Matching text in a stream** — `ContentMatch.search` reads a file through
  `IFileSystem` buffer by buffer and answers whether the text occurs, without ever
  holding the file whole. The encoding is settled first with the same
  `TextEncoding.detect` the viewer calls, over the same first
  `TextEncoding.ProbeLength` bytes — a byte order mark, then the zero pattern of
  UTF-16, then a NUL meaning Latin-1, then valid UTF-8 — so a UTF-16 file is
  searched as text rather than as bytes that happen to spell nothing, and a file
  is always searched in the encoding the viewer would show it in. Whole words
  means the characters either side of a match are not a letter, a digit or `_`.
  A match that straddles two buffers is found, which the tests prove at every
  offset around the boundary; a match that spans a line break is not looked for.
  Whether a file is binary is not a second guess made here: `TextEncoding` gains
  it as part of what `detect` answers, and the rule is the branch `detect`
  already has — no byte order mark, no UTF-16 pattern, and a NUL in the probe —
  so the search and the viewer can never disagree about a file, and the viewer's
  header can say so whenever that is wanted. A binary file is skipped unless the
  option asks for it, in which case it is searched as the Latin-1 the viewer
  shows it as. "Not
  containing" is the negation of the answer, and a file that cannot be read is
  neither — it is counted as skipped, so an unreadable file never passes as one
  that lacks the text. Each buffer asks the control whether the search was
  stopped. *Done when:* ASCII, UTF-8 and UTF-16 files, the buffer boundary, whole
  words, case, the binary rule and an unreadable file are each covered on
  `MockFileSystem`.
- **P14.4 Walking the disk** — `FileSearch.run` walks the places a scope names
  through `IFileSystem` and reports as it goes: every hit, with its full path and
  the entry the listing would give for it, and every directory it enters, which is
  what the summary line says the walk is doing. `FromDirectory` walks the tree
  below the panel's directory, `DirectoryOnly` that directory's entries alone, and
  `MarkedDirectories` the tree below each marked directory — a marked file is
  ignored, and with no directory marked the scope cannot be chosen. A directory
  that refuses to be read is skipped and counted, the way `DirectoryMeasure`
  counts it. Hidden entries follow the query's option, and a hidden directory
  that is not searched is not walked into either. A directory link is a leaf
  unless the query follows links; followed, it is entered only if the real path
  it resolves to has not been entered before, so a link to its own ancestor
  finishes. The walk answers `Finished` or `Stopped` with its counts of files,
  directories and skipped entries. *Done when:* a nested tree on `MockFileSystem`
  answers its hits for each of the three scopes, the hidden option and an
  unreadable directory are covered, and a cycle through a link terminates on
  `Fakes.LinkFollowing` with each directory reported once.
- **P14.5 The volume, the drives and `PATH`** — the three scopes that do not start
  at the panel. `FromVolumeRoot` starts at the root `Drives.holding` names for the
  panel's location — the disk holding the archive when the panel stands inside
  one — and does not cross into a directory that is itself the root of another
  listed drive, so `/` does not wander into `/media/user/stick`; the mounts
  P13.12 filters out are never listed and so never entered. `AllDrives` walks
  each row of `Drives.list` in its order, the home row excepted, since it lies on
  one of the drives already walked. `PathDirectories` walks the directories of
  `PATH` without descending: `HostEnvironment` gains them, read and split in
  `Adapters` at the platform's separator — `;` on Windows, `:` on Unix — with an
  entry that does not exist left out and a directory named twice walked once.
  *Done when:* each scope is covered on `Fakes.MountedFileSystem` with a second
  mount, and the `PATH` reading is covered for both separators.
- **P14.6 Inside the archives** — with the option on, an archive the walk meets
  is a hit candidate by its own name like any file, and then its index is read
  once through `ArchiveReader` and every entry at every level is matched by name;
  with text, each file entry is streamed into `ContentMatch`, which for a tar.gz
  means decompressing it whole, a price the option says it pays. An archive
  inside an archive is matched by name and not entered. An archive that needs a
  password is skipped and counted rather than asked about, since a disk full of
  them would put one prompt per archive between the user and the answer. A hit
  inside an archive carries the archive's `FileSystemPath` and an `ArchivePath`,
  and a panel already standing inside an archive searches below that level with
  `FromDirectory` and `DirectoryOnly` whether the option is on or not. *Done
  when:* a zip and a tar.gz on `MockFileSystem` answer hits by name and by text at
  several levels, a nested archive is not entered, and a protected one is counted
  as skipped.
- **P14.7 The question popup** — `Alt+F7` opens *Find* over the active panel: the
  masks, the text, the scope as an option selector and the options as check
  boxes, greyed out where P14.2 says they mean nothing — the text options while
  the text is empty, directories while it is not, marked directories while none
  is marked. The hidden box opens on what the panel conceals. A *Last results*
  button, which P14.10 fills, is there and disabled. From a panel showing search
  results (P14.11) the directory the scopes start from is the one that panel
  returns to. `Esc` changes nothing, and what the popup answers is a `FindQuery`
  or nothing. Read off a rendered screen, not off a property of the view. *Done
  when:* the popup answers every scope and option, the greying follows the typed
  text and the marks, and it stays inside an 80-column terminal.
- **P14.8 The results window** — the query runs off the UI thread through a
  `RunSearch` port, and a full-screen window shows what it finds as it finds it.
  It is a `Window` with no buttons, run modally the way `Prompts.showFile` runs
  the viewer, because P14.1 measured that a `Dialog` answers `Enter` with its
  default button before a handler sees it; what the window offers is reached by
  keys, and the key bar at its foot says which.
  Hits reach the window through `app.Invoke` gathered on a short timer rather than
  one call per hit, which is the rough edge the progress window already records.
  The list is grouped by the directory a hit lives in: a header row carrying the
  directory's path — `C:\work\backup.zip\docs` for a level inside an archive — and
  the hits under it, a found directory being a row in the group of its parent.
  Groups appear in the order the walk reached them and are never re-sorted while
  it runs, so a new hit never moves the row under the cursor. A row shows the
  name, the size and the date. The summary line counts files, directories and
  skipped entries and says `Searching: <directory>` while the walk runs,
  `Finished` once it is done and `Stopped` when it was stopped; an answer with no
  hits says so in words. The first `Esc` stops a running search and leaves the
  window up over what it found; the next one closes it. *Done when:* a search
  over a tree shows its hits grouped while it runs, the summary follows the walk
  through all three states, and two presses of `Esc` stop and then close.
- **P14.9 `Enter` and `F3` from the results** — `Enter` on a hit closes the
  window and puts the active panel in the directory holding it with the cursor on
  it, whether it is a file or a directory; a hit the panel conceals turns the
  panel's hidden entries on first, the way `Alt+H` would, since the user just
  asked to stand on it. A hit inside an archive walks the panel into that level of
  the archive. `Enter` on a group header puts the panel inside that directory.
  `F3` on a file hit opens the viewer over the results window — a hit inside an
  archive streamed as P9.11 streams it, asking for the password first — and
  `Esc` in the viewer returns to the results with the cursor where it was; `F3`
  on a directory does nothing. Whatever the window answers goes through `Update`
  as a message, together with the hits and the cursor, which P14.10 keeps. *Done
  when:* `Enter` lands on a file, a directory, a concealed entry, an entry inside
  an archive and a group's directory, each as a test over the update, and `F3`
  views a hit and returns to the list.
- **P14.10 The last results** — `AppState.LastSearch` holds the query, the scope's
  starting place, the hits, whether the walk finished or was stopped and the
  row the cursor was on when the window closed. It belongs to the session and
  not to a tab, is replaced by the next search, and is not written to the
  settings file. The *Last results* button of P14.7 reopens it, carrying the
  count — `Last results (123)` — and is disabled while there is nothing to
  reopen. Reopening checks that every hit still exists, drops the ones that do
  not and says how many went, and the summary line names the query and where it
  ran, so a list from half an hour ago cannot pass for a fresh one. The cursor
  comes back on the row it left, or on the nearest one still there. *Done when:*
  `Enter` on a hit, `Alt+F7` and *Last results* land the cursor on that hit
  again, a file deleted in between is gone from the list and counted, and a new
  search replaces the old answer.
- **P14.11 The results in the panel** — a key in the results window — the bar
  names it, and it is a key rather than a button for the reason P14.1 gives —
  sends the answer into the active panel. `PanelLocation` gains a third case,
  `SearchResults`, holding the hits and the location the panel stood on before,
  and the listing is those hits as entries: the name column shows each hit's path
  relative to where the search started, or the full path when the scope had more
  than one start (`AllDrives`, `PathDirectories`, several marked directories),
  because two `README.md` from two directories must not look like one. `..` goes
  back to the location stood on before, with the cursor where it was then. The
  title says `Find: *.jpg in C:\photos`. Sorting, hidden entries, marking, the
  quick search, the colours and the footer work on it as on a directory. `Enter`
  on a directory goes into that real directory and leaves the results behind;
  `Enter` on a file runs it as P10.3 does, and `F3` views it. `Ctrl+R` checks the
  hits still exist and drops the ones that do not. The panel is not watched,
  since a result can span more directories than a watch should hold. The session
  file keeps the location stood on before, a new tab opened beside it opens on
  that location rather than on the results, and `Alt+F7` from the panel searches
  from that location. *Done when:* the hits show in the panel with their relative
  paths, `..` returns to the directory stood on before with its cursor, and a
  restart and `Alt+F11` both land on that directory.
- **P14.12 Operations on the results in the panel** — the operation paths of a
  results panel come from each entry's own path instead of from the panel's
  directory joined to a name, so the marks can lie in as many directories as the
  search found them in. `F4`, `F5`, `F6` as a move, `F8`, `Shift+F8` and `Alt+Del`
  act on them as they act on a directory's entries, into the other panel's
  directory where they have a target. Everything that needs the panel itself to
  be a directory refuses by name before a popup opens: `F7`, `Shift+F4`,
  `Shift+F1`, `Shift+F2`, `Alt+F6`, `F6` given a bare name as a rename, and any
  key that would use the results panel as the *target* of the other panel's
  operation. What was moved, deleted or wiped leaves the listing afterwards and
  the marks of what failed stay. Hits inside archives refuse in this task with a
  sentence saying so, which P14.13 removes. *Done when:* marked hits from three
  directories copy, move, delete and wipe in one batch, each refusal is covered
  by a test over the update, and the listing afterwards holds what is still there.
- **P14.13 Operations on the results inside archives** — the marks of a results
  panel may lie on the disk and in several archives at once, and every operation
  so far assumes one source place. The operation therefore splits its targets by
  the place holding them — the disk, or one particular archive — and runs each
  group through the path that place already has: copying on the disk, extracting
  out of an archive for `F5`, the extract-then-drop of P9.13 for `F6`, and
  removing entries for `F8`. All of the groups run in one batch behind one
  progress window, `Esc` stopping the whole of it, a password is asked once per
  archive that needs one, and the capability gate is asked per group: a group in
  a kind that cannot be changed is refused by the archive's name, and the rest of
  the batch still runs. *Done when:* a mark on the disk, one in a zip and one in
  a rar copy out together, moving the same three moves two and refuses the rar
  by name, and the listing afterwards holds what is still there.
- **P14.14 The documentation** — `Architecture/FINDING-FILES.md` says what a query
  is and why `*.*` is read the classic way, how text is matched and why binary
  files are an option, each scope and what it refuses to cross, why the hits live
  in the window and not in the state while the walk runs, the last results and
  the results panel; [Links](Architecture/LINKS.md) gains the one walk that may
  follow a link and the set that keeps it from spinning;
  [State and effects](Architecture/STATE-AND-EFFECTS.md) gains `LastSearch` and
  the third `PanelLocation`; [UI and input](Architecture/UI-AND-INPUT.md) gains
  the two windows and their keys; [Cross-platform](Architecture/CROSS-PLATFORM.md)
  gains the `Alt+F7` row, the `PATH` separator and the mount boundary; and the
  user guide gains the command. *Done when:* the phase is described where the
  rest of the design is, not only in this plan.

### Left for a later phase

- **History.** The query popup opens empty every time in this phase. Remembering
  the last masks, text, scope and options in the settings file, and a history of
  earlier masks and texts to pick from, is its own phase — and the natural home
  for keeping `LastSearch` across a restart as well.
- **Excluding masks** in the classic spelling, `*.* | *.bak`.
- **A depth limit** on the walk.
- **Regular expressions**, and text that spans a line break.
- **The viewer opening at the match**, with the text highlighted, and a line
  preview of the match in the results window.

## Phase 15 — Answering the reviews

Goal: act on the three reviews of 2026-09-19 — [architecture](reports/2026-09-19-architecture-review.md),
[code quality](reports/2026-09-19-code-quality-review.md) and [performance](reports/2026-09-19-performance-review.md) — in an order where
every task leaves the code easier to change for the tasks after it. Findings are
cited by their ids in those reports (`A…`, `Q…`, and `perf P…` for the
performance report, so they are not read as task numbers); the phase adds no
feature, so every task keeps the behaviour the suite already describes and adds
the tests that pin what it fixes.

Three rules decide the order. Correctness comes first: the findings that can
lose or confuse a user's data are small and independent, and nothing else should
wait for them. The refactorings of `Update` and `Shell` come next, because
almost every later task passes through those two files, and a merged `Effect` or
a single gate turns a change made twice into a change made once. Performance
goes from the hottest path down — every keystroke, every search, then the large
inputs — and the I/O model follows once the archive reader has a scope that a
cache and a stream can lean on. Readability work comes last, so no arm is
polished that a structural task is about to delete.

### Stage 1 — Correctness

- **P15.1 An item result knows where it came from** — `Operations.ItemResult`
  identifies an item by its source (`FileSystemPath`, or the archive and the
  entry's `ArchivePath`) rather than by its bare `EntryName`, and
  `AppState.keepUnfinished`, `Archive.droppingArrived` and
  `Archive.carryingOver` key their maps on it (A6). A results panel already
  mixes directories, so two hits named alike must no longer trade outcomes.
  *Done when:* two marked hits named `x.txt` in different directories, one of
  which fails, leave exactly the failed one marked, and moving out of an archive
  drops only the entry that arrived.
- **P15.2 One rule for "this path lies under that one"** — a single
  `FileSystemPath.isUnder` that takes the platform's case rule replaces
  `Archive.within`, `Drives.holding`, `SearchListing.relativeTo`, the boundaries
  of `FileSearch.run`, `FindGround.sameRoot` and the comparison in
  `Links.volumeOf`, and `FileSearch.visited` compares with the same rule (A7,
  TD-020 in part). Whether the boundary is strict is in the name, not in the
  body (Q6 in part). *Done when:* `[<Theory>]` rows for both platforms cover
  equal, strictly-below and differently-cased paths, and packing a directory
  into a differently-cased sibling on Linux is no longer refused.
- **P15.3 Moving a directory is a move first** — `Operations.moveTo` tries
  `Directory.Move` and falls back to copy-then-delete only when the move is
  refused across volumes (perf P5, first half). Renaming a directory in place
  and `F6` on one volume become metadata operations and can no longer leave two
  trees behind. *Done when:* a fake that counts copies shows none for a
  same-volume move, and a fake that refuses the move shows the fallback.
- **P15.4 No silent failures** — `Settings.save` answers a `Result` that
  `QuitRequested` and `SessionSaveRequested` report; `BatchRunner.runMeasuring`
  and `SearchListing.heldIn` answer an error instead of an empty measurement or
  an unverified hit (A9, Q11); `BatchRunner.drive` and `SearchRunner.run`
  dispose what they create (A13). `Archive.needsPassword` waits for P15.13,
  which answers it from the index. *Done when:* each of the four failures is a
  test that sees a reported error.

### Stage 2 — `Update` and `Shell` made ready

- **P15.5 No primed aliases** — the forty `let x' = x fs` bindings at the top of
  `Update.transition` go, and each arm passes `fs` itself (Q1, perf P10);
  `sentToRoot` loses `loadPanel'` and `getRoot`. Mechanical, and first, so that
  every later diff in `Update` reads plainly. *Done when:* `Update.fs` has no
  primed name and the suite passes unchanged.
- **P15.6 The results gate is decided once** — the refusal that `guarding`
  computes today is a value derived from the message and the two panels before
  `transition` runs, and the seven arms lose their continuation and one level of
  indentation (Q7, TD-021). *Done when:* `guarding` is gone, the rule reads in
  one place, and every refusal from P14.12 still has its test.
- **P15.7 Three effects instead of thirteen** — `CopyPlaces`, `MovePlaces` and
  `DeletePlaces` stay and the ten single-place cases go; `Update` builds a
  one-group list where it built a special case, and `Shell.interpret` keeps only
  the `Piece` machinery (A2). The unchangeable-archive refusal becomes the one
  behaviour the places path has. *Done when:* `Effect` has no single-place
  transfer case, `CopyRequested` and `MoveRequested` have one arm per target
  kind, and an unchangeable archive is refused the same way from a directory
  panel and from a results panel.
- **P15.8 `Runtime` drains a queue** — `Dispatch` enqueues, and one loop runs
  update, render and interpret until the queue is empty, so an effect always
  sees the state it was raised from and a nested dispatch cannot overtake the
  outer one (A3). *Done when:* a test dispatching from inside an effect sees the
  outer effects run first and the `WatchLocations` of the latest state
  interpreted last.

### Stage 3 — The hot paths

- **P15.9 A keystroke renders what changed** — `PanelState.Entries` becomes an
  array, `FilePanel.render` remembers the last state per side and skips the
  rows, colours and marks when `Entries` and `Selected` are the same references,
  a cursor-only change moves `SelectedItem` and refreshes the footer, and a
  modifier change repaints the bottom bar alone (perf P1, A14, perf P14). The
  sort computes each key once (perf P12). The integration tests that read a
  listing by index are adjusted in the same task (Q14). *Done when:* a test over
  a counting renderer shows no row rebuilt for `CursorMoved` and none for the
  untouched panel, and the result is rendered and checked on screen.
- **P15.10 The results window appends** — a tick appends the new hits' rows to
  the existing collection, a group header only when the location changes; the
  counts arrive with the report from `FileSearch.run`; `walking` updates the
  summary line alone; the row under the cursor is kept by identity, not by its
  `ToString()` (perf P2, Q8 in part). *Done when:* a test feeding ten ticks
  formats each hit once, and a search is rendered and checked on screen.
- **P15.11 A hit is found by lookup** — the display name is stored on `FindHit`
  when the results are shown, and `SearchListing.hitFor` becomes a lookup, so
  the operation paths, `keepUnfinished`, `transferSource` and `deleteScope` are
  linear in the marks (perf P3). *Done when:* the results-panel tests of P14.12
  and P14.13 pass unchanged over the indexed lookup.
- **P15.12 The results are verified when asked** — a results panel checks its
  hits when it is first shown and on `Ctrl+R`, and not when the other panel's
  directory changes (perf P4). *Done when:* an external change in the opposite
  panel asks the file system nothing about the hits.

### Stage 4 — The I/O model

- **P15.13 An archive is read within a scope** — `ArchiveReader.withItems`
  replaces the lazily evaluated items: the metadata (`ArchiveEntryInfo`, with an
  `EntryKind` instead of two booleans) may be kept, the content is handed out
  only inside the scope (A5, Q8 in part), and the legacy encoding is resolved
  once per process (perf P13). The index of the archive under a panel is cached
  by path, length and last write time, and levels, `needsPassword` and `heldIn`
  are answered from it (perf P7 in part, A9 for `needsPassword`). *Done when:*
  keeping an item past its scope does not compile, and changing level inside an
  archive reads the file once.
- **P15.14 The walks enumerate once** — `FileSearch.run` is split so that the
  archive scan and the level bookkeeping take explicit inputs and answer
  explicit results (Q10); then one helper enumerates a directory's children as
  `IFileSystemInfo`, and `Operations.deleteTree`, `copyTo`, `wipe`,
  `DirectoryMeasure`, `FileSearch` and `Archive.add` walk over it (perf P6,
  TD-019, Q5 `below`). *Done when:* a counting fake shows one enumeration and no
  extra stat per entry for every walk.
- **P15.15 Nothing long runs on the UI thread** — permanent delete, trash and a
  directory rename that fell back to copying run through `RunBatch` with a
  progress window and `Esc` (perf P7, A8, perf P5 second half); `deleteScope`
  and the reads that scale with the data become effects answering with a
  `…Loaded` message (A1). The architecture documents then say which transitions
  may touch `IFileSystem` and which work is an effect, and `TESTING.md` stops
  contradicting itself (A12). *Done when:* each of those keys shows its progress
  window on a large tree, and `OVERVIEW.md` matches the code.
- **P15.16 Archive entries are streamed** — the viewer and the content search
  read an archive entry as a stream inside the scope of P15.13 instead of
  buffering it whole (perf P8). The rewrite of 7z and tar.gz keeps its
  documented buffering. *Done when:* viewing and searching an entry larger than
  the memory budget of a test stream succeed.
- **P15.17 Solid 7z extraction is measured** — `Shift+F2` on a 7z of a few
  thousand small entries is timed against the reference tool; only if the time
  grows quadratically does extraction move to the sequential reader path the tar
  kinds already use (perf P9). *Done when:* the measurement is recorded in
  `PROGRESS.md`, with the change or the reason none was needed.

### Stage 5 — Readability

- **P15.18 The ports are grouped and typed** — `Shell.Dependencies` splits into
  prompts, processes, watches and runners; each prompt takes a request record
  and answers a typed value, so two prompts can no longer be wired to each
  other; `FindChoice` moves next to `FindQuery`; `Prompts` runs every dialog
  through one helper (A4, Q5 `running`, Q8 prompt ports). *Done when:* no two
  prompts share a type and `Fakes.dependencies` overrides a field of a
  sub-record.
- **P15.19 Flat decisions** — file by file, three-deep `match` cascades become a
  match over a tuple or a computation expression, parenthesis chains become
  pipelines, and a chain of three binds or one nested match becomes `result {}`
  or `option {}` (Q2, Q3, Q4). The cut-off goes into
  [Development](DEVELOPMENT.md). *Done when:* the sites listed in Q2-Q4 that
  survived the earlier tasks are rewritten and the rule is written down.
- **P15.20 Names that tell the boundary** — the remaining prepositional names
  and the `changing` / `opened` families say what they answer (Q6). *Done when:*
  no two helpers in one module differ only by a prime or a preposition.
- **P15.21 The remaining copies and flags** — one generic lookup of a dialog's
  sub-view by id, one `..` entry, one helper for a lazily replaced mock member
  in `Fakes` (Q5); `ArchiveCapabilities` as a set, `LinkDialog` keeping its
  kinds, `FindDialog` ids not built from `%O`, and the remaining booleans (Q8).
  *Done when:* each copy listed in Q5 has one definition.
- **P15.22 `PanelState` stands on its own** — the panel manipulation that never
  touches `fs` moves out of `AppState` into its own module with its own tests
  (A11). *Done when:* `AppState.fs` holds loaders, `finish*` transitions and the
  queries `Update` asks.
- **P15.23 The rest, as files are touched** — `Domain` free of `System.IO` types
  (A10), dense one-liners split into named predicates (Q9), partial access
  (Q12), the small style inconsistencies (Q13), the line index and the viewer on
  very large files (perf P11); the wipe's generator stays as documented (perf
  P15). *Done when:* every finding of the three reports is either done or
  recorded in [Tech debt](TECH-DEBT.md) with a reason, and `TD-019`, `TD-020`
  and `TD-021` are closed.

## Suggested module layout

```
Marbas/
  FileSystem/        P2, P5 — listing, operations (no UI dependencies)
  State/             P3, P4 — panel & app state, selection, Store
  Ui/                P1+    — panels, dialogs, key bindings, theming;
                              since grouped into feature folders, see
                              Architecture/OVERVIEW.md
  Program.fs         composition root
```

Dependencies flow downward only: `Ui` → `State` → `FileSystem`.

### State handling

`AppState` is immutable: every transition is a pure function returning a new
state, which is what the unit tests exercise. The single mutable cell lives in
`State/Store.fs` — a small store that Terminal.Gui event handlers use to
`Dispatch` those pure transitions and re-render afterwards. Handlers never
mutate state directly.

Terminal.Gui runs a single-threaded event loop, so the store needs no
synchronisation; background work (P5.6) must marshal its dispatch back onto
the UI thread via `Application.Invoke`.
