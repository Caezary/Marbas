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
  with Norton Commander key bindings as the reference.
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
  ordering matches Norton Commander conventions.
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

Goal: mark files the way NC does, as input for file operations.

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

- **P7.1 NC-style color scheme** — classic blue panels, cyan selection;
  respects terminals with limited colors. *Done when:* the app is
  recognizably Norton-ish.
- **P7.2 Sorting options** — `Ctrl+F3..F6`-style toggles: name / extension /
  date / size, ascending & descending, per panel. *Done when:* sort mode is
  switchable and shown in the panel.
- **P7.3 Hidden files toggle** — show/hide dotfiles and hidden/system
  entries. *Done when:* toggle works on both Windows and Unix conventions.
- **P7.4 Persisted settings** — panel paths, sort modes, and toggles saved
  to a config file and restored on start. *Done when:* restart preserves the
  session.

## Phase 8 — Quality & release

- **P8.1 Test suite consolidation** — unit tests for listing, sorting,
  selection and operations modules run in CI-friendly fashion
  (`dotnet test`). *Done when:* green test run covers core logic.
- **P8.2 Cross-platform verification** — manual checklist on Windows and
  Linux (paths, drives, permissions, terminal quirks). *Done when:*
  checklist documented in `_documentation/` and passing.
- **P8.3 Packaging** — publish profiles for self-contained single-file
  builds per OS. *Done when:* `dotnet publish` produces a runnable binary.

## Suggested module layout

```
Marbas/
  FileSystem/        P2, P5 — listing, operations (no UI dependencies)
  State/             P3, P4 — panel & app state, selection
  Ui/                P1+    — panels, dialogs, key bindings, theming
  Program.fs         composition root
```

Dependencies flow downward only: `Ui` → `State` → `FileSystem`.
