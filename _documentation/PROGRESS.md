# Progress

Status as of 2026-08-09. Phases and task numbers refer to [PLAN.md](PLAN.md).

## Where we are

**Phases 1–4 are complete and P5.1–P5.5 are done** (21 of 29 tasks). Marbas runs
as a full-screen two-panel file browser: both panels list a real directory, the
keyboard moves between and inside them, entries can be marked one by one or by
wildcard for later operations, a status line describes the entry under the
cursor, and failures surface as dialogs instead of crashes. The operations
module can copy, move, rename, delete and create directories, and every
function key of Phase 5 except the progress dialog is wired: `F5` copies and
`F6` moves or renames into the other panel, `F7` makes a directory and `F8`
deletes, all of them acting on the same operation targets. What is left of the
phase is P5.6 — running long operations off the UI thread.

**278 tests pass**, covering every logic module directly and the UI behaviour by
driving real Terminal.Gui views over an in-memory file system.

| Phase | Tasks | State |
|-------|-------|-------|
| 1 — Application skeleton | P1.1–P1.4 | done |
| 2 — Directory listing | P2.1–P2.4 | done |
| 3 — Navigation | P3.1–P3.5 | done |
| 4 — Selection | P4.1–P4.3 | done |
| 5 — File operations | P5.1–P5.6 | P5.1–P5.5 done, P5.6 next |
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
- `F8` deletes the operation targets of the active panel — the marked entries,
  or the entry under the cursor when nothing is marked, never `..`. It first
  asks for confirmation, naming a single target or counting several, with an
  extra warning when a target directory is not empty; saying no changes
  nothing. Directories go with everything inside them, the panel refreshes with
  the marks of the deleted entries dropped, and whatever failed is listed
  entry by entry in one dialog while the rest of the batch still goes through.
- `F5` copies the same operation targets into the directory shown by the other
  panel. Nothing is asked while nothing collides; a target that already exists
  raises a Yes / No / All / Cancel question naming it, where `No` skips that
  one entry, `All` stops asking for the rest of the batch and `Cancel` drops
  whatever is left. Directory trees go over whole. Afterwards both panels are
  re-read, the marks of the copied entries are dropped while the marks of the
  ones that failed stay, and failures are listed entry by entry.
- `F6` opens a prompt filled with the other panel's path. Accept it and the
  operation targets move there — same collision question as `F5`, same refresh
  and per-item reporting afterwards. Type a bare name instead and, when a
  single entry is being acted on, it is renamed where it stands, with the
  cursor following it to its new place in the listing; a name typed while
  several entries are marked is read as a directory beside them. An empty
  answer or a cancelled prompt does nothing.
- `Ctrl+Q` or `F10` quits. The bottom bar lists the Norton-style function keys
  (`F5 Copy`, `F6 RenMov`, `F7 Mkdir`, `F8 Delete`, `F10 Quit`) and every one
  of them now does its job.

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
    Update.fs             messages and effects: message + state -> state + effects
    Runtime.fs            generic host: the single mutable cell, dispatch, render
  Ui/
    EntrySize.fs          size column text, byte format injected per view
    EntryFormat.fs        one entry -> one fixed-width row
    SelectionSummary.fs   marked entries -> panel summary text
    CursorInfo.fs         cursor entry -> status line text
    StatusLine.fs         the status line view
    ListingErrorMessage.fs  ListingError -> user-facing text
    OperationErrorMessage.fs  OperationError (and failed items) -> user-facing text
    DeleteConfirmation.fs delete targets -> confirmation question
    OverwriteQuestion.fs  collision -> question text, answer buttons and mapping
    FilePanel.fs          panel views, render state -> view
    FunctionKeyBar.fs     bottom shortcut bar, one action per function key
    PanelInput.fs         panel key -> message
    Shell.fs              Dependencies and the effect interpreter
    PromptDialog.fs       single-field text prompt (label + default)
    PatternDialog.fs      the `+` / `-` pattern prompt, over PromptDialog
    MainWindow.fs         composition: views, runtime, event wiring
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
functions; `Update.update` folds a `Message` into a new state plus a list of
`Effect` values, and the generic `Runtime` holds the only mutable reference,
re-renders after each dispatch and hands the effects to the interpreter.
Handlers send messages, they never mutate. See PLAN.md, *State handling*.

**The outside world is injected.** `Shell.Dependencies` names every port (file
system, start path, quit callback, error reporter, drive chooser, pattern
prompt, name prompt, confirmation) and `Shell.interpret` is the only place that
turns an `Effect` into a call on one of them.
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

**Overwriting is a flag on one item, a question on a batch.** `copy`/`move`
take a bool and refuse with `TargetExists` when it is false. `copyAll` no
longer takes that bool: it takes an `ask: FileSystemPath -> OverwriteAnswer`
and drives the Yes / No / All / Cancel loop itself, because that loop is
application logic and belongs next to the file system, not in a view.
`Shell.interpret` only supplies the callback that opens the dialog — the same
shape as `IFileSystem` being injected. (P5.1 had assumed the UI would run the
loop item by item; asking `ask` for the decision turned out to keep both sides
simpler.)

**Copy and move are the same batch with a different verb.** `copyAll` and
`moveAll` are two lines over a shared private `allAsking`, which owns the
Yes / No / All / Cancel walk; only the operation applied to each source
differs. A rename is not part of it: it touches one entry, so it stays a plain
`Result` carried by `RenameFinished` instead of a one-element batch.

**A bare name means rename, anything else means a directory.** `F6` suggests
the other panel's path, and what comes back decides the operation:
`FileSystemPath.isBareName` (no directory part and not rooted) plus exactly one
target means rename in place; otherwise the text is a directory — combined with
the source panel's path, so a relative name lands beside the entries and an
absolute one is taken as it stands. That keeps one edit box doing both jobs
without a mode switch, the way Norton Commander does it.

**A rename moves the cursor with the entry.** `finishRename` reloads focusing
`ByName newName`, so the renamed entry keeps the cursor even though the listing
reorders around it. Every other reload focuses whatever the cursor was on.

**One question per item covers the whole tree.** `copyAll` asks only when the
*top-level* target already exists — and if it does not, nothing inside it can
exist either, so no conflict can appear deeper in the recursion. The answer
therefore decides the item as a whole: `No` skips it before anything is
written, so a refusal can never leave a half-copied tree behind, and `Yes`
copies the tree with overwriting on, without asking again for every file
inside. Skipped and cancelled items are simply absent from the results, which
is what keeps their marks in place.

**Marks survive exactly what did not finish.** After a batch, the panel keeps
the marks of the entries whose `ItemResult` is not `Ok` (and that still exist);
everything that succeeded loses its mark. Delete, copy and move share the rule,
so a failed batch can be retried by pressing the key again.

**Moving a directory is copy-then-delete.** `IDirectory.Move` fails across
volumes, which is the normal case for a two-panel manager, so directories are
copied and then removed; files still use `IFile.Move`.

**Function keys live on the `StatusBar`, not the panel.** A `StatusBar`
shortcut swallows its key globally (see the `F10` trap), so a panel-level
`KeyDown` handler for `F7` would never fire. `FunctionKeyBar.create` therefore
takes an `Actions` record — one callback per function key — and `MainWindow`
supplies the handlers. Their messages carry no side or index — a `StatusBar`
shortcut has no notion of focus, so `CopyRequested` and its siblings read the
active panel and its cursor from the state, which the panel events keep up to
date.

**`ReportError` carries a title.** Listing failures and operation failures want
different dialog captions, so the dependency is `title -> message -> unit`;
`ListingErrorMessage`/`OperationErrorMessage` each own their `Title`, and
`Program.fs` just forwards both to the message box.

**Operation errors are mapped, not re-thrown, inside a transition.**
`AppState.makeDirectory` creates the directory and then reloads the panel; the
reload returns a `ListingError`, which a private pure mapping folds into an
`OperationError` so the whole transition answers with one error type. State
still never depends on Ui — the mapping is over the raw error values.

**Operation targets are one rule, shared by every operation key.**
`AppState.operationTargets` answers with the marked entries in listing order,
or with the cursor entry when nothing is marked, and never with `..` — so a
function key pressed on the parent entry has nothing to do and asks nothing.
`F5`, `F6` and `F8` all take their sources from it.

**A confirmation is an effect; its wording belongs to Ui.** `ConfirmDelete`
carries the target names and a single flag saying whether any target directory
has contents, and `DeleteConfirmation.describe` turns that into the question.
The emptiness check runs in the state layer, where the file system already is
(`Operations.isNonEmptyDirectory`); a directory that cannot be enumerated
counts as non-empty, so the warning errs towards showing up.

**Per-item failures share one dialog.** `Operations.deleteAll` answers for every
target, `Operations.failures` keeps the ones that went wrong, and the
`ReportItemFailures` effect renders them through
`OperationErrorMessage.describeFailures` as one `name: reason` line each. The
panel is refreshed first, so what survived a partial failure is on screen
before the dialog appears.

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
also writes `ListView.SelectedItem`, which raises the same event, so `Runtime`
raises its `IsRendering` flag while it renders and the handler ignores events
raised inside it. Without the flag, render and handler bounce off each other.

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

**P5.6** — progress and cancellation: `Shell.interpret` currently runs
`copyAll` / `moveAll` straight through on the UI thread, so a large tree
freezes the window. The batch has to move to a background thread with a
progress dialog and `Esc` cancellation, marshalling its dispatch back through
`Application.Invoke` (see PLAN.md, *State handling*). The `ask` callback the
batch already takes is the model for the progress callback; the harder part is
that the overwrite question must then be asked from the background thread.

## Known rough edges

- Both panels start on the same directory, so `F5` straight after launch copies
  a file onto itself and the OS refuses it with an error dialog. `F6` can be
  pointed elsewhere by hand, `F5` cannot — a target prompt for `F5`, or a check
  that the two directories differ, would be the fix.
