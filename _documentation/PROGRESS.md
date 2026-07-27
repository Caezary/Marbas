# Progress

Status as of 2026-07-21. Phases and task numbers refer to [PLAN.md](PLAN.md).

## Where we are

**Phases 1–4 are complete and P5.1–P5.2 are done** (18 of 29 tasks). Marbas runs
as a full-screen two-panel file browser: both panels list a real directory, the
keyboard moves between and inside them, entries can be marked one by one or by
wildcard for later operations, a status line describes the entry under the
cursor, and failures surface as dialogs instead of crashes. The operations
module can copy, move, rename, delete and create directories; `F7` is the first
key wired to it — it makes a directory in the active panel. The remaining
operation keys arrive in P5.3–P5.6.

**149 tests pass**, covering every logic module directly and the UI behaviour by
driving real Terminal.Gui views over an in-memory file system.

| Phase | Tasks | State |
|-------|-------|-------|
| 1 — Application skeleton | P1.1–P1.4 | done |
| 2 — Directory listing | P2.1–P2.4 | done |
| 3 — Navigation | P3.1–P3.5 | done |
| 4 — Selection | P4.1–P4.3 | done |
| 5 — File operations | P5.1–P5.6 | P5.1–P5.2 done, P5.3 next |
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
- `+` and `-` ask for a wildcard pattern and mark or unmark everything it
  matches, so `*.fs` is one step. Matching ignores case and never touches `..`.
- The bottom row of each panel summarises its marks — how many entries and how
  many bytes they add up to — and stays empty while nothing is marked.
- A status line above the function-key bar describes the entry under the cursor
  of the active panel — untruncated name, exact size in bytes (`<DIR>` for
  directories) and modification date. It follows both cursor movement and
  panel switching.
- `F7` prompts for a name and creates that directory in the active panel; the
  panel refreshes with the cursor on the new entry and existing marks intact. A
  blank name or a cancelled prompt does nothing; a clash (or any failure) is
  reported in a dialog.
- `Ctrl+Q` or `F10` quits. The bottom bar lists the Norton-style function keys
  (`F5 Copy`, `F6 RenMov`, `F7 Mkdir`, `F8 Delete`, `F10 Quit`); `F7` and `F10`
  are wired up so far.

## Code map

```
Marbas/
  FileSystem/
    FileSystemPath.fs     single-case path type wrapping a raw string
    DirectoryListing.fs   entries, ordering, ".." handling, errors as values
    Drives.fs             mounted drives
    NamePattern.fs        wildcard matching for `+` / `-`
    Operations.fs         copy / move / rename / delete / mkdir, errors as values
  State/
    AppState.fs           immutable panel/app state and its transitions
    Store.fs              the single mutable cell + dispatch
  Ui/
    EntrySize.fs          size column text, byte format injected per view
    EntryFormat.fs        one entry -> one fixed-width row
    SelectionSummary.fs   marked entries -> panel summary text
    CursorInfo.fs         cursor entry -> status line text
    StatusLine.fs         the status line view
    ListingErrorMessage.fs  ListingError -> user-facing text
    OperationErrorMessage.fs  OperationError -> user-facing text
    FilePanel.fs          panel views, render state -> view
    FunctionKeyBar.fs     bottom shortcut bar, one action per function key
    PromptDialog.fs       single-field text prompt (label + default)
    PatternDialog.fs      the `+` / `-` pattern prompt, over PromptDialog
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

**Paths are a type, not a string**. `FileSystemPath` is a
single-case union with a private constructor and a companion module; the
fs-dependent helpers (`combine`/`name`/`directory`/`parent`) take `IFileSystem`
so path handling stays consistent with `MockFileSystem`. It flows through the
listing, operations, and panel/navigation layers and the error path fields.
Raw OS strings are wrapped once at the outside edge (`Program.fs`, `MainWindow`
start path and drive roots).

**Immutable state, one mutable cell.** `AppState` transitions are pure
functions; `Store` holds the only mutable reference and re-renders after each
dispatch. Handlers send transitions, they never mutate. See PLAN.md,
*State handling*.

**The outside world is injected.** `MainWindow.create` takes a `Dependencies`
record (file system, start path, quit callback, error reporter, drive chooser,
pattern prompt).
`Program.fs` is the only place that opens real dialogs, which keeps
dialog-triggering behaviour testable. The dialogs themselves are built in `Ui`
modules (`PatternDialog`) so their layout can be tested too — a field placed
outside the dialog's content area is invisible yet still typable, which no
behaviour test would notice.

**No code comments.** Intent lives in names, tests and these documents.

**Operations answer per item, and one bad item never stops the batch.**
`Operations` mirrors `DirectoryListing`: every function returns
`Result<unit, OperationError>` instead of throwing, and the `*All` functions map
a list of source paths to `ItemResult` values (entry name plus outcome) in the
input order, so the UI can report exactly which entries failed and why. A
single-item failure inside one *tree* does abort that tree — a half-copied
directory stops at the first error rather than pressing on blindly.

**Overwriting is a flag, not a policy.** `copy`/`move` take a bool and refuse
with `TargetExists` when it is false. The Yes / No / All / Cancel decision of
P5.4 belongs to the UI, which calls the operation per item with the answer it
already has.

**Moving a directory is copy-then-delete.** `IDirectory.Move` fails across
volumes, which is the normal case for a two-panel manager, so directories are
copied and then removed; files still use `IFile.Move`.

**Function keys live on the `StatusBar`, not the panel.** A `StatusBar`
shortcut swallows its key globally (see the `F10` trap), so a panel-level
`KeyDown` handler for `F7` would never fire. `FunctionKeyBar.create` therefore
takes an `Actions` record — one callback per function key — and `MainWindow`
supplies real handlers (`ignore` for the not-yet-built ones). The mkdir action
reads `store.State.Active` to target whichever panel is current, since a
`StatusBar` shortcut has no notion of focus.

**`ReportError` carries a title.** Listing failures and operation failures want
different dialog captions, so the dependency is `title -> message -> unit`;
`ListingErrorMessage`/`OperationErrorMessage` each own their `Title`, and
`Program.fs` just forwards both to the message box.

**Operation errors are mapped, not re-thrown, inside a transition.**
`AppState.makeDirectory` creates the directory and then reloads the panel; the
reload returns a `ListingError`, which a private pure mapping folds into an
`OperationError` so the whole transition answers with one error type. State
still never depends on Ui — the mapping is over the raw error values.

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
- `ListView` type-ahead search swallowed `+` and `-` before any handler saw
  them; the panel disables it with `KeystrokeNavigator <- null`, which also
  matches Norton Commander, where letters never move the cursor.

## Next up

**P5.3** — `F8` delete: a confirmation dialog (with an extra warning for a
non-empty directory), then a recursive delete of the operation targets, errors
reported per item.

Still missing before the operation keys can act on a selection: a state
function giving the operation targets — the marked entries in listing order,
or the cursor entry when nothing is marked. `AppState.selectedEntries` covers
only the first half today. `Operations.deleteAll` already takes a list of
paths, so P5.3 needs that target list plus a confirm dialog dependency.
