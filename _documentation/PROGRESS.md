# Progress

Status as of 2026-07-20. Phases and task numbers refer to [PLAN.md](PLAN.md).

## Where we are

**Phases 1–3 are complete and Phase 4 is under way** (15 of 29 tasks). Marbas
runs as a full-screen two-panel file browser: both panels list a real
directory, the keyboard moves between and inside them, entries can be marked
for later operations, a status line describes the entry under the cursor, and
failures surface as dialogs instead of crashes. Nothing modifies the file
system yet — that is Phase 5.

**92 tests pass**, covering every logic module directly and the UI behaviour by
driving real Terminal.Gui views over an in-memory file system.

| Phase | Tasks | State |
|-------|-------|-------|
| 1 — Application skeleton | P1.1–P1.4 | done |
| 2 — Directory listing | P2.1–P2.4 | done |
| 3 — Navigation | P3.1–P3.5 | done |
| 4 — Selection | P4.1–P4.2 done, P4.3 next | in progress |
| 5 — File operations | P5.1–P5.6 | not started |
| 6 — Viewing files | P6.1–P6.2 | not started |
| 7 — Polish & configuration | P7.1–P7.4 | not started |
| 8 — Quality & release | P8.1–P8.3 | not started |

## What works today

- Full-screen window split into two panels, resizing with the terminal.
- `Tab` switches panels; the focused one is marked with a double border.
- Both panels list a real directory: `..` first, then directories, then files,
  each group alphabetically, in aligned name / size / date columns that
  reformat when the terminal is resized. Directories show `<DIR>`.
- `Enter` descends into the directory under the cursor. `Enter` on `..` goes
  up and places the cursor on the directory just left.
- Unreadable or vanished directories raise a message box; the panel stays put.
- `Ctrl+R` reloads the active panel, keeping the cursor on the same entry even
  when its position changed.
- `Alt+F1` / `Alt+F2` open a drive chooser for the left / right panel.
- `Insert` or `Space` marks the entry under the cursor and moves one entry
  down; pressing it again unmarks. Marked entries are highlighted, `..` can
  never be marked, and each panel keeps its own marks — they survive `Ctrl+R`
  (minus whatever disappeared) and are dropped when the panel changes
  directory.
- A status line above the function-key bar describes the entry under the cursor
  of the active panel — untruncated name, exact size in bytes (`<DIR>` for
  directories) and modification date. It follows both cursor movement and
  panel switching.
- `Ctrl+Q` or `F10` quits. The bottom bar lists the Norton-style function keys
  (`F5 Copy`, `F6 RenMov`, `F7 Mkdir`, `F8 Delete`, `F10 Quit`); only `F10` is
  wired up so far.

## Code map

```
Marbas/
  FileSystem/
    DirectoryListing.fs   entries, ordering, ".." handling, errors as values
    Drives.fs             mounted drives
  State/
    AppState.fs           immutable panel/app state and its transitions
    Store.fs              the single mutable cell + dispatch
  Ui/
    EntrySize.fs          size column text, byte format injected per view
    EntryFormat.fs        one entry -> one fixed-width row
    CursorInfo.fs         cursor entry -> status line text
    StatusLine.fs         the status line view
    ListingErrorMessage.fs  ListingError -> user-facing text
    FilePanel.fs          panel views, render state -> view
    FunctionKeyBar.fs     bottom shortcut bar
    MainWindow.fs         composition: views, store, key handling
  Program.fs              real file system and real dialogs
```

Dependencies flow downward only: `Ui` → `State` → `FileSystem`.

## Decisions worth remembering

**Terminal.Gui 2.4 over Spectre.Console and Consolonia.** Only Terminal.Gui is
a full TUI framework (windows, focus, event loop) with a large, active
community and an MIT licence. Its v2 API is instance-based
(`Application.Create()`); the static `Application` model is deprecated.

**Test first.** Every task started with failing tests. Logic modules are pure
and take `IFileSystem`, so tests run against `MockFileSystem` and never touch
the disk.

**Immutable state, one mutable cell.** `AppState` transitions are pure
functions; `Store` holds the only mutable reference and re-renders after each
dispatch. Handlers send transitions, they never mutate. See PLAN.md,
*State handling*.

**The outside world is injected.** `MainWindow.create` takes a `Dependencies`
record (file system, start path, quit callback, error reporter, drive chooser).
`Program.fs` is the only place that knows about real dialogs, which keeps
dialog-triggering behaviour testable.

**No code comments.** Intent lives in names, tests and these documents.

**One rule for the size column, formatting per view.** `EntrySize.describe`
holds the shared decision — directories get `<DIR>`, files get their size, an
unknown size gets nothing — and takes the byte-to-text function as an argument.
The panel row passes `EntrySize.plain` (bare digits, to keep columns narrow),
the status line passes a grouped format with a `bytes` suffix. A new view, or a
future switch to `KB`/`MB`, only supplies another function.

**Selection is a set of names in the state; the ListView only displays it.**
`PanelState.Selected` holds entry names, not indices, so marks stay on the
right files when a refresh reorders the listing. Terminal.Gui's own marking is
used purely for rendering: the panel runs with `MarkMultiple = true` and
`ShowMarks = false`, which draws marked rows with `VisualRole.Highlight` and no
checkbox glyphs, and `FilePanel.render` re-applies `Source.SetMark` from the
state after every render. `Insert`/`Space` are handled by Marbas and marked
handled, so the built-in SPACE marking never runs and cannot disagree with the
state.

**The cursor lives in the state, and rendering is guarded.** The panel's
`ListView.ValueChanged` dispatches `AppState.setCursor`, so state always knows
where the cursor is (P4.2 will build the selection on top of it). Rendering
also writes `ListView.SelectedItem`, which raises the same event, so
`MainWindow` sets a `rendering` flag while it renders and the handler ignores
events raised inside it. Without the flag, render and handler bounce off each
other.

## Traps already hit (details in [TESTING.md](TESTING.md))

- `StatusBar` shortcuts swallow their key even with a `null` action — `F10`
  looked bound but did nothing until it got a real callback.
- `Key` is mutable: sending a shared instance sets `Handled` on it forever, so
  later sends are silently ignored. Tests send fresh copies via
  `TestHelpers.press`.
- Keys do not bubble from a nested view to its parent, so handlers live on the
  focused `ListView`.
- `View.AddCommand` is `protected`; custom shortcuts use `KeyDown` handlers.

## Next up

**P4.3** — count and total size of the marked entries shown in the panel, plus
`+`/`-` selecting and deselecting by wildcard pattern. `AppState.selectedEntries`
already returns the marked entries in listing order, which is what the summary
and the Phase 5 operations will consume.
