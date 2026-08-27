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

Later in this phase, once creating works: unpacking the archive under the
cursor into the other panel, and walking into an archive as though it were a
directory. Both read formats Marbas cannot write — `rar` above all, whose
compression is proprietary and which therefore never appears among the formats
an archive can be created in.

### Libraries

`SharpZipLib` (MIT) writes zip, because it is the only maintained managed
library that can encrypt one; `SharpCompress` (MIT) writes tar.gz and, later,
7z, and reads everything including rar. Both are fully managed, which keeps
the single-file publish of P8.3 intact, and neither is copyleft. Wrappers
around the native `7z.dll` are LGPL and were rejected on both counts.

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
