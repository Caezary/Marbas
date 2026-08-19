# Progress

Status as of 2026-08-19. Phases and task numbers refer to [PLAN.md](PLAN.md).

## Where we are

**All eight phases are complete** (32 of 32 tasks). Marbas
runs as a full-screen two-panel file browser: both panels list a real directory,
the keyboard moves between and inside them, entries can be marked one by one or
by wildcard for later operations, a status line describes the entry under the
cursor, and failures surface as dialogs instead of crashes. Every function key
does its job — `F3` views the file under the cursor and `F4` opens
it in the system editor, `F5` copies and `F6` moves or renames into the other
panel, `F7` makes a directory, `F8` deletes — the operations all acting on the
same operation targets, with copying and moving running off the UI thread behind
a progress window that `Esc` stops. Since P7.1 the screen wears the classic
colours: blue panels with cyan entries, a cyan cursor bar, yellow marks and a
cyan function-key bar, and since P7.2 each panel sorts by name, extension, date
or size in either direction, saying so in its title. P7.5 put every key binding
into one table: the bottom bar is a row out of it, and holding `Ctrl` or `Alt`
swaps that row for the commands those modifiers reach. Since P7.3 each panel
also decides on its own whether the hidden entries — dotfiles and whatever the
OS marks hidden or system — are listed at all. P7.4 closed the phase: where each
panel stands, how it sorts and whether it conceals are written to a config file
when the app quits and read back when it starts, so a session survives a
restart. P8.1 put a number on the suite and a workflow around it, and P8.2 made
both hold on Linux as well as on Windows. P8.3 closed the plan: two publish
profiles turn the project into one self-contained, single-file executable per
operating system, and a tag builds both on their own runners and hangs them off
a GitHub release.

**Since the plan closed, one round of panel work** answered five things a user
met straight away: the cursor no longer falls out of a panel at its last entry,
the left and right arrows jump by page instead of switching panels, entries are
coloured by what they are, the bottom row of a panel is separated by a rule and
says what the directory holds, and typing a letter searches the panel.

**587 tests pass on Windows; on Linux 584 pass and 3 skip** — the three that
assert on drive letters, which exist nowhere else. 433 are unit tests over the
logic modules and the pure `Ui` ones, plus 154 integration tests driving real
Terminal.Gui views over an in-memory file system. All of them follow the
arrange/act/assert shape and the folder layout described in
[TESTING.md](TESTING.md). `dotnet test Marbas.slnx` is the whole run, and
GitHub Actions runs it on both operating systems on every push and pull request;
measured with `coverlet.runsettings` it covers 99% of the lines and 95% of the
branches of everything but `Program.fs`. What no test can reach — colours, a
resized terminal, a real permission bit, the mouse — is written up as the
checklist in [CROSS-PLATFORM.md](CROSS-PLATFORM.md).

| Phase | Tasks | State |
|-------|-------|-------|
| 1 — Application skeleton | P1.1–P1.4 | done |
| 2 — Directory listing | P2.1–P2.4 | done |
| 3 — Navigation | P3.1–P3.5 | done |
| 4 — Selection | P4.1–P4.3 | done |
| 5 — File operations | P5.1–P5.6 | done |
| 6 — Viewing files | P6.1–P6.2 | done |
| 7 — Polish & configuration | P7.1–P7.5 | done |
| 8 — Quality & release | P8.1–P8.3 | done |

## What works today

- Full-screen window split into two panels, resizing with the terminal.
- `Ctrl+F3`, `Ctrl+F4`, `Ctrl+F5` and `Ctrl+F6` sort the panel they are pressed
  in by name, extension, date or size; pressing the key of the sort already in
  use reverses it. `..` stays first and directories stay above files whatever
  the order, entries the key cannot tell apart fall back to their names, the
  cursor keeps the entry it was on, marks are untouched, and the panel title
  says which sort is on (`C:\start [size ↑]`). Each panel sorts on its own and
  keeps its order through every reload and every directory it is pointed at.
- `Alt+H` (or the classic `Ctrl+H`, where the terminal delivers it) shows or
  hides the hidden entries of the panel it is pressed in — a
  name starting with a dot, or an entry the OS marks hidden or system. They are
  concealed until asked for, they take their usual place in the order once shown
  (`.config` sits among the files, not above them), and the title says so
  (`C:\start [name ↑ hidden]`). The cursor keeps the entry it was on and the
  marks of the entries that stay survive; a mark on an entry that has just been
  concealed is dropped, so nothing can be operated on unseen. Each panel decides
  on its own, and it keeps the decision through every reload and every directory
  it is pointed at. `..` is never concealed, whatever the parent directory is
  marked with.
- `Tab` switches panels; the focused one is marked with a double border, and
  nothing else moves between them. `↑`/`↓`, `PgUp`/`PgDn` and `Home`/`End`
  stop at the first and the last entry instead of falling out of the panel.
- `→` moves the cursor to the last entry of the page on screen and, pressed
  again, a page forward onto the last entry of the next one; `←` does the same
  backwards, first to the top of the page and then a page back. Both stop at
  the ends of the listing and neither changes panel. The page is the one
  actually on screen — the jump is measured from the list's own scroll offset,
  so it follows a panel that has been scrolled.
- Typing a printable character opens a search in the active panel: the bottom
  row shows `Search: …` and the cursor moves to the first entry whose name
  contains the text, wherever in the name and whatever the case. `Backspace`
  shortens it and closes it on the last character, `Esc` closes it, and any
  other key closes it and then does its own job — so `Enter` opens what was
  found and `F5` copies it. While the search is open every printable key is
  text, `Space` and `+` included; while it is closed those keep their commands.
  Switching panels or reloading a panel closes its search, and the cursor stays
  where the search left it.
- Entries are coloured by what they are: directories and `..` white, ordinary
  files bright blue, archives bright magenta and executables bright green. A
  marked entry stays yellow and the entry under the cursor keeps the cursor
  bar, so neither is repainted.
- Both panels list a real directory: `..` first, then directories, then files,
  each group alphabetically, in aligned name / size / date columns that
  reformat when the terminal is resized. Directories show `<DIR>`.
- `Enter` descends into the directory under the cursor. `Enter` on `..` goes
  up and places the cursor on the directory just left.
- Unreadable or vanished directories raise a message box; the panel stays put.
- `Ctrl+R` reloads the active panel, keeping the cursor on the same entry even
  when its position changed.
- `Alt+F1` / `Alt+F2` open a root chooser for the left / right panel — the ready
  drives on Windows, the file-system root and the home directory on Unix.
- `Insert` or `Space` marks the entry under the cursor and moves one entry
  down; pressing it again unmarks. Marked entries are highlighted, `..` can
  never be marked, and each panel keeps its own marks — they survive `Ctrl+R`
  (minus whatever disappeared) and are dropped when the panel changes
  directory.
- `+` and `-` ask for a wildcard pattern and mark or unmark everything it
  matches, so `*.fs` is one step. Matching ignores case and never touches `..`.
- A rule separates the listing from the bottom row of each panel, and that row
  is never blank: it summarises the marks — how many entries and how many bytes
  they add up to — and falls back to what the directory holds (`3 files   1,234
  bytes`, counting files only) while nothing is marked. An open search takes it
  over from both.
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
  raises a Yes / No / Yes to all / No to all / Cancel question naming it, where
  `No` skips that one entry, `Yes to all` and `No to all` settle every later
  collision the same way without asking again, and `Cancel` drops whatever is
  left. Directory trees go over whole. Afterwards both panels are
  re-read, the marks of the copied entries are dropped while the marks of the
  ones that failed stay, and failures are listed entry by entry.
- `F6` opens a prompt filled with the other panel's path. Accept it and the
  operation targets move there — same collision question as `F5`, same refresh
  and per-item reporting afterwards. Type a bare name instead and, when a
  single entry is being acted on, it is renamed where it stands, with the
  cursor following it to its new place in the listing; a name typed while
  several entries are marked is read as a directory beside them. An empty
  answer or a cancelled prompt does nothing.
- While `F5` or `F6` works, a progress window names the file being handled and
  the rest of the app keeps drawing, because the batch runs on a background
  thread. `Esc` (or the Cancel button) closes that window and the batch stops
  at the next file: whatever was already copied stays, an entry caught in the
  middle is listed afterwards as stopped before it finished, and a move that
  was interrupted never deletes its source. Every overwrite question is
  answered before the work starts, so nothing can block waiting for the user.
- `F3` opens the file under the cursor — only that one, never the marked
  entries — in a read-only, scrollable viewer titled with its path, which `F3`
  or `Esc` closes. A directory or `..` under the cursor opens nothing. Text is
  shown line by line whatever its line endings; a file holding a zero byte is
  shown as a hex dump instead; only the first megabyte is read, and a file
  longer than that says so on its last line. A file that cannot be read (it
  vanished, or the OS refused) is reported in a dialog.
- `F4` opens the same file — the one under the cursor — in the system editor:
  whatever `$EDITOR` names, otherwise `notepad` on Windows and `vi` elsewhere.
  Marbas waits for the editor to close, then re-reads the panel with the cursor
  and the marks where they were, so a file saved (or created) meanwhile is on
  screen straight away. An editor that cannot be started is reported in a
  dialog; a directory or `..` under the cursor starts nothing.
- The screen wears the classic colours: both panels are blue with their entries and
  frames in bright cyan, the cursor bar of the focused panel is black on cyan,
  marked entries and the summary line beneath them are bright yellow, the two
  rows under the panels are light grey on black, and the function-key bar is
  black on cyan. Every colour is one of the sixteen ANSI ones, so a terminal
  with a limited palette shows them as they are.
- `Ctrl+Q` or `F10` quits. The bottom bar lists the classic function keys
  (`F3 View`, `F4 Edit`, `F5 Copy`, `F6 RenMov`, `F7 Mkdir`, `F8 Delete`,
  `F10 Quit`) and every one of them now does its job.
- The bar follows the modifiers being held: with `Ctrl` down it lists the sort
  commands (`F3 Name`, `F4 Ext`, `F5 Date`, `F6 Size`), with `Alt` down the
  root choosers and the hidden toggle (`F1 Left`, `F2 Right`, `H Hidden`),
  and it goes back to the classic keys
  when they are let go. A modifier no command asks for shows an empty bar.
  Every entry can also be clicked. On Windows the modifiers are read from the
  keyboard ten times a second; elsewhere the bar stays on the classic row.
- Quitting writes the session to `%APPDATA%\Marbas\settings.conf` (on Unix,
  `~/.config/Marbas/settings.conf`): where each panel stands, which key it sorts
  by and in which direction, and whether it conceals the hidden entries. The
  next start reads it back, so both panels come up on the directories they were
  left on, sorted the way they were sorted. The file is `key=value` lines meant
  to be readable and editable by hand; anything it does not say, says wrongly or
  spells in a way that means nothing falls back to what a first start would use,
  and a directory that is no longer there falls back to the working directory
  rather than raising an error at startup. A missing file is a first start. When
  the file cannot be written at all, the app quits without complaining.

## Code map

```
Marbas/
  FileSystem/
    EntryName.fs          single-case type for the name of one entry
    FileSystemPath.fs     single-case path type wrapping a raw string
    SortOrder.fs          which key a panel sorts by, in which direction
    HiddenEntries.fs      what makes an entry hidden, and whether a panel lists it
    Executability.fs      what makes an entry a program, by name and by mode bit
    SearchText.fs         the text a panel is being searched for, and what it matches
    Settings.fs           the session both panels carry, and its config file
    DirectoryListing.fs   entries, ordering, ".." handling, errors as values
    Roots.fs              where a panel can be sent: drives, or root and home
    NamePattern.fs        wildcard matching for `+` / `-`
    Operations.fs         copy / move / rename / delete / mkdir, errors as values
    FileContent.fs        one file's content: text lines or bytes, errors as values
  State/
    AppState.fs           immutable panel/app state and its transitions,
                          built from and read back as Settings
    Update.fs             messages and effects: message + state -> state + effects
    Runtime.fs            generic host: the single mutable cell, dispatch, render
  Ui/
    ClassicScheme.fs      the four schemes the screen is painted with
    Modifiers.fs          Ctrl / Alt / Shift as a set, and the set a key carries
    KeyMap.fs             every key binding: key, bar label, message
    EntrySize.fs          size column text, byte format injected per view
    EntryFormat.fs        one entry -> one fixed-width row
    EntryColor.fs         one entry -> its colour group, and that group's attribute
    PageJump.fs           where the left and right arrows land, from the page on screen
    SelectionSummary.fs   marked entries -> panel summary text
    PanelFooter.fs        panel state -> the one line under the rule
    CursorInfo.fs         cursor entry -> status line text
    StatusLine.fs         the status line view
    HexDump.fs            bytes -> offset / hex / text rows
    FileView.fs           file content -> the lines the viewer shows
    ListingErrorMessage.fs  ListingError -> user-facing text
    ContentErrorMessage.fs  ContentError -> user-facing text
    OperationErrorMessage.fs  OperationError (and failed items) -> user-facing text
    DeleteConfirmation.fs delete targets -> confirmation question
    OverwriteQuestion.fs  collision -> question text, answer buttons and mapping
    PanelTitle.fs         path and sort order -> the panel frame's title
    FilePanel.fs          panel views, render state -> view
    FunctionKeyBar.fs     bottom bar: the KeyMap row of the modifiers held
    Shell.fs              Dependencies and the effect interpreter
    PromptDialog.fs       single-field text prompt (label + default)
    PatternDialog.fs      the `+` / `-` pattern prompt, over PromptDialog
    ProgressDialog.fs     the window shown while a batch runs
    FileViewer.fs         the full-screen read-only viewer behind `F3`
    EditorCommand.fs      which editor `F4` starts, per platform and `$EDITOR`
    MainWindow.fs         composition: views, runtime, event wiring
  Program.fs              real file system and real dialogs
  Properties/PublishProfiles/
    win-x64.pubxml        self-contained single-file build for Windows
    linux-x64.pubxml      the same for Linux

Marbas.Tests/
  Root.fs                 the root every test path is built on, per platform
  WindowsFact.fs          a Fact that skips where drive letters do not exist
  Expect.fs               Result unwrapping, so act and assert stay apart
  Fakes.fs                the do-nothing Shell.Dependencies every test starts from
  FileSystem/             unit tests, one module per FileSystem module
  State/                  unit tests, one module per State module
  Ui/                     unit tests of the pure Ui modules and the Shell interpreter
  Integration/            every test that constructs a Terminal.Gui view
    Gui.fs                focusedWindow, leftList, rightList, press, panel,
                          panelTitle, shownPath

coverlet.runsettings      what coverage measures and what it leaves out
.github/workflows/
  ci.yml                  restore, build and test on Windows and Linux
  release.yml             publish both profiles on a tag, attach them to a release
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
Raw OS strings are wrapped once at the outside edge — `Program.fs` for the
start path, `Shell` for drive roots and prompt answers.

**Every value a `Message` or `Effect` carries says what it is**.
`EntryName` for an entry's name, `NamePattern` for a wildcard, `FileSystemPath`
for a path, `SelectionMode` (`Select`/`Deselect`) and `DeleteScope`
(`NamedEntriesOnly`/`WithDirectoryContents`) instead of unnamed booleans, and
`PanelCursor` bundling the side with the row it points at. Two things stayed
primitive on purpose: the cursor `Index` inside `PanelCursor`, because an
ordinal position is what an `int` is for, and `TargetEntered`'s text, which is
read before `Update` decides whether it names an entry or a directory.
Constructors follow one rule — `create` is total, `tryCreate` validates and
answers an `option`. `EntryName` has both, because the file system hands over
names that are names by construction while a prompt hands over text that may
be blank; `NamePattern` has only `tryCreate`. Trimming and the blank check live
in `tryCreate`, so `Shell` binds it onto the prompt answer and a blank answer
becomes indistinguishable from a cancelled one before any message exists.

**Immutable state, one mutable cell.** `AppState` transitions are pure
functions; `Update.update` folds a `Message` into a new state plus a list of
`Effect` values, and the generic `Runtime` holds the only mutable reference,
re-renders after each dispatch and hands the effects to the interpreter.
Handlers send messages, they never mutate. See PLAN.md, *State handling*.

**The outside world is injected.** `Shell.Dependencies` names every port (file
system, start path, quit callback, error reporter, root chooser, pattern
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

**Every question is answered before the batch starts.** `Operations.approve`
walks the sources, asks Yes / No / Yes to all / No to all / Cancel about each
one whose target already exists, and answers with the sources to actually work
on — `No` drops one, `Cancel` drops the lot, since nothing has happened yet.
The two *to all* answers become a `StandingAnswer` that stands in for the
question from then on, so a later collision is settled without asking while a
source that collides with nothing is still taken. What runs
afterwards is decision-free: `copy` and `move` carry no overwrite flag any more
and always replace, because whoever called them has already decided. The only
refusal left lives in `rename`, which checks its own target. (P5.1 had the
flag, P5.4 had the batch asking as it went; P5.6 made both untenable, because a
background thread cannot open a dialog and wait on it.)

**Copy and move are the same batch with a different verb.** `copyAll` and
`moveAll` are two lines over a shared private `allWatched`, which owns the walk
and the cancellation check; only the operation applied to each source differs.
A rename is not part of it: it touches one entry, so it stays a plain `Result`
carried by `RenameFinished` instead of a one-element batch.

**A bare name means rename, anything else means a directory.** `F6` suggests
the other panel's path, and what comes back decides the operation:
`FileSystemPath.isBareName` (no directory part and not rooted) plus exactly one
target means rename in place; otherwise the text is a directory — combined with
the source panel's path, so a relative name lands beside the entries and an
absolute one is taken as it stands. That keeps one edit box doing both jobs
without a mode switch, the way the orthodox managers do it.

**A rename moves the cursor with the entry.** `finishRename` reloads focusing
`ByName newName`, so the renamed entry keeps the cursor even though the listing
reorders around it. Every other reload focuses whatever the cursor was on.

**One question per item covers the whole tree.** `approve` asks only when the
*top-level* target already exists — and if it does not, nothing inside it can
exist either, so no conflict can appear deeper in the recursion. That is what
makes asking up front complete rather than merely convenient: one answer
settles the item as a whole, every file inside it included, and a refusal stops
it before anything is written. Sources that were dropped never appear in the
results, which is what keeps their marks in place.

**The background thread is a port, not a thread.** `Dependencies.RunBatch`
takes the progress title, the work to do (handed a `BatchControl` to report
through and to ask whether it should stop) and what to do with the results.
`Program.fs` is the only place that knows about `Task.Run`, `app.Invoke` and
running the progress dialog modally; the tests pass a runner that does the work
inline, which is why every `F5`/`F6` test stayed synchronous and none of them
had to change when the operations moved off the UI thread.

**Cancelling means "the progress window is gone".** The token is cancelled the
moment `app.Run dialog` returns, whatever the reason. `Esc` closes the dialog,
so the batch stops at its next file; if instead the batch finished first, its
`Invoke` callback closes the dialog and the cancel that follows changes
nothing. The same flag guards the stop request, so a batch that finishes after
the window is already gone cannot stop the main window by mistake — which
would quit the application.

**A stopped entry is reported, not passed over in silence.** Cancelling inside
a tree leaves it half-copied, so the item answers `Cancelled path`, counts as a
failure and is listed with the others; its mark stays, because it never
finished. Entries the batch never reached are simply absent. A move that was
interrupted never deletes its source: the delete is sequenced after the copy in
the same `result` block, so a failed copy skips it.

**Marks survive exactly what did not finish.** After a batch, the panel keeps
the marks of the entries whose `ItemResult` is not `Ok` (and that still exist);
everything that succeeded loses its mark. Delete, copy and move share the rule,
so a failed batch can be retried by pressing the key again.

**Moving a directory is copy-then-delete.** `IDirectory.Move` fails across
volumes, which is the normal case for a two-panel manager, so directories are
copied and then removed; files still use `IFile.Move`.

**No message carries a cursor except the one that moves it.** `CursorMoved` is
the only message with a `PanelCursor`; `EntryOpened`, `SelectionToggled`,
`SortRequested`, `RefreshRequested` and the pattern pair say what happened and
nothing else, and `Update` reads the active panel and its cursor from the state.
That is sound because the state is never behind: `ListView.ValueChanged`
dispatches `CursorMoved` on every move and `HasFocusChanged` dispatches
`PanelFocused`, so by the time a key is pressed the state already knows where
the cursor stands. It is also what lets one binding serve both a key press and a
mouse click on the bar, since a click has no notion of a cursor. `F5`, `F6` and
`F8` were built this way from the start; P7.5 made the rest follow.

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
carries a `DeleteRequest` — the target names plus a `DeleteScope` saying
whether the delete stops at those entries (`NamedEntriesOnly`) or reaches
inside a directory as well (`WithDirectoryContents`) — and
`DeleteConfirmation.describe` turns that into the question. `AppState.deleteScope`
decides it in the state layer, where the file system already is
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

**`MainWindow` is a composition root, `AppView` is the screen.** `AppView.create`
builds the window, the two panels and the status line and hands back a record;
`AppView.render` is the whole screen as a function of `AppState`, so it can be
tested by rendering a state into a view and reading the panels back, with no
`Runtime` involved. The side-to-view pairing lives in `FilePanel.Panels` and
`FilePanel.pairs`, which is what render, seeding and wiring all iterate.
`MainWindow.create` is left with four steps: build the view, build the runtime,
seed both panels, attach the wiring. The function key bar is added after the
runtime exists, because it needs `dispatch` — which needs the runtime, which
needs the render function, which needs the views.

**Reading a file answers what it is, not what it looks like.**
`FileContent.read` hands back a `Content` — a `ContentBody` that is either
`Text` lines or the raw `Binary` bytes, plus a `ContentExtent` saying whether
the file fits in what was read. Which of the two it is comes from one rule: a
zero byte anywhere in what was read means binary, because text encodings do not
carry one. Turning that into rows on a screen is Ui's job — `HexDump.describe`
formats bytes as offset / hex / text, `FileView.lines` picks between the two
bodies and appends the truncation notice. `FileSystem` therefore stays free of
formatting and the hex layout can be tested without a terminal.

**The viewer reads at most a megabyte.** A file manager is pointed at whatever
is under the cursor, disk images included, so `read` opens a stream, takes
`FileContent.MaxBytes` at most and answers `Truncated` when there was more.
That keeps `F3` from pulling gigabytes into memory on the UI thread. The
notice on the last line is what tells the user they are not seeing everything.

**`F3` and `F4` take the cursor entry, not the operation targets.** Copy, move
and delete work on `operationTargets` — the marks, or the cursor when nothing
is marked. Viewing and editing are a different act: each opens one file, so
both read `AppState.cursorFilePath`, which also filters out directories and
`..`. There is nothing to view or edit for a directory, so nothing happens.

**The editor is a port, and its failure is an operation failure.**
`Dependencies.RunEditor` takes the path and answers
`Result<unit, Operations.OperationError>`; `Program.fs` is the only place that
knows about `Process.Start`, `WaitForExit` and repainting the screen
afterwards, and it maps whatever the start threw through
`Operations.mapException`. That reuses the error type, the
`ReportOperationError` effect and `OperationErrorMessage` rather than inventing
a second failure vocabulary for one case — an editor that will not start failed
on a path, like every other operation error. The tests pass a `RunEditor` that
records the path and can write to the mock file system, which is what makes
"the panel shows what the editor left behind" an ordinary test.

**Which editor to start is a pure choice.** `EditorCommand.choose` takes
`$EDITOR` as an option and a `Platform` (`Windows`/`Unix`) and answers with the
command: what was configured, trimmed, or `notepad`/`vi`. `Program.fs` only
reads the environment and asks. A blank `$EDITOR` counts as unset.

**A finished edit re-reads the panel it started from.** `EditFinished Ok` runs
the same `AppState.refresh` that `Ctrl+R` does, so the cursor stays on the file
and the marks survive; only the active panel is re-read, because that is the
directory the editor was pointed into.

**Closing the viewer is injected, because `RequestStop` needs a running app.**
`Runnable.RequestStop` does nothing unless the dialog is being run by an
application, so a test cannot observe it. `FileViewer.create` therefore takes
the closing action as its first argument — `Program.fs` passes
`_.RequestStop()`, the tests pass a recorder — which leaves "F3 and Esc close
the viewer, other keys do not" as an ordinary assertion. The key handler sits
on the `TextView` because that is the view holding focus, and it marks the key
handled so the `F3` shortcut on the main window's status bar cannot reopen a
viewer on top of the one being closed.

**One table holds every key, and the bar is a view of it.** `KeyMap.bindings`
is the whole keyboard: key, an optional bar label, and the `Message` the key
stands for. `KeyMap.messageFor` is what the panel's single `KeyDown` handler
asks, and `KeyMap.row modifiers` is what the bottom bar draws — the labelled
bindings whose key carries exactly those modifiers, in table order. Adding a
command is one line, and a command cannot be on the bar under one name and do
something else when its key is pressed, because both come from the same row.
There was a `PanelInput` module holding the panel half of this; P7.5 merged it
into `KeyMap` and deleted it.

**The bar shows keys, it no longer takes them.** Every `Shortcut` is built with
`BindKeyToApplication <- false`, so it still renders `F5 Copy` and still answers
to a mouse click, but the key itself is handled where every other key is — the
`KeyDown` handler on the focused panel's `ListView`. That kills the trap that
made `F7` unreachable from the panel, and it means a function key does nothing
while a dialog holds focus, which is what it should do anyway. The price is that
tests can no longer send `F5` to the window and have the bar catch it, so
`Gui.pressKey` sends it to the panel that has focus, the way a user does.

**Held modifiers are polled, because the driver will not report them.**
Terminal.Gui 2.4.17 has the vocabulary for it — `Key.IsModifierOnly`,
`Key.ModifierKey` — but only the kitty keyboard protocol fills it in. The
Windows input processor returns from every key-up before anything is raised
(`if (!inputEvent.KeyEvent.bKeyDown) { return; }`) and never surfaces a
standalone `Ctrl`, so there is no event to listen to on the platform Marbas is
developed on. `Dependencies.WatchModifiers` is therefore a port that hands over
a callback; `Program.fs` is the only place that knows about `GetAsyncKeyState`
and `app.AddTimeout`, polls ten times a second and reports only when the set
changes. On anything but Windows it registers nothing and the bar keeps the
classic row. Everything above the port — which row a set of modifiers means,
what the bar then shows — is ordinary tested code, because tests call the
captured callback themselves.

**A row is what the table says, empty included.** Holding `Shift` shows an empty
bar, because no command is bound to `Shift`. Falling back to the classic row
would say that `Shift+F5` copies, which it does not.

**Panel event wiring is its own module, attached after the panels are seeded.**
`PanelWiring.attach` subscribes one panel's `ListView` and frame to a
`PanelWiring.Host` — dispatch, repaint, and the "am I rendering?" question —
so `MainWindow` only builds views and hands over that host, and the wiring can
be tested against a bare `FilePanel` with a recording host instead of a whole
window. `MainWindow` keeps two loops over the panels on purpose: the first
dispatches the startup `PathOpened`, the second attaches the wiring. While the
initial state is being seeded no handler exists yet, so nothing can re-enter
the runtime — the `IsRendering` guard covers `ValueChanged`, but the
`FrameChanged` handler calls `Runtime.Refresh` directly, and a nested `paint`
lowers the flag for the render that is still running. Merging the loops would
put that hazard into startup.

**Colour is four schemes in one module; the views only ask for them.**
`ClassicScheme` holds `screen`, `panel`, `summary` and `keyBar` as
`Terminal.Gui` `Scheme` values, and four `SetScheme` calls put them on the
window, the two panel frames, the summary label and the status bar. Nothing
else in `Ui` mentions a colour. A scheme is inherited by every sub-view that
has none of its own, which is why the entry list and the status line need no
call: they take the scheme of the panel and of the window. Swapping the palette
is one file, and P7.4 can persist a choice of scheme without touching a view.

**Sixteen colours, on purpose.** Every attribute is built from `ColorName16`,
the DOS palette the classic managers themselves had, so nothing has to be
approximated on a terminal that offers no more than sixteen — which is what "respects
terminals with limited colours" comes down to. A test walks every colour the
schemes set and asserts it is exactly one of the sixteen, so a hand-picked RGB
value cannot slip in unnoticed.

**One comparison, three layers.** `DirectoryListing.order` compares a group
rank first — `..`, then directories, then files — and only inside a group does
the sort key decide; entries the key cannot tell apart fall back to their names.
The direction reverses the key and that fallback together, but never the group
rank, which is why a descending sort still keeps `..` on top and directories
above files, the way the classic managers do it. Sorting by name is the same
rule with an empty key comparison, so it needs no case of its own.

**Re-sorting never touches the disk.** `AppState.sortBy` is a pure transition:
it switches the panel's `SortOrder`, re-orders the entries already in the panel
and moves the cursor to wherever the entry it was on landed, leaving the marks
alone. Nothing is re-read, so a sort cannot fail and cannot be refused by a
directory that vanished meanwhile. What keeps the order afterwards is the
`SortOrder` the panel carries: `loadPanelFocusing` hands it to `listForDisplay`,
so a refresh, a navigation, a drive switch and every reload after an operation
all come back sorted the way the panel is set.

**The same key again reverses.** `SortOrder.switchTo` starts ascending on a key
the panel is not sorted by and flips the direction on the key it is, so
`Ctrl+F3..F6` are the whole vocabulary and no separate direction key exists.

**The title says which sort is on.** `PanelTitle.describe` puts the sort order
after the path (`C:\start [size ↑]`), and `FilePanel.render` is the only caller.
Since P7.3 the same bracket carries the hidden toggle when it is on
(`C:\start [name ↑ hidden]`), so one indicator holds everything the panel is set
to and no test outside `PanelTitleTests` has to know its wording. The tests that
care about the directory a panel shows read it back through `Gui.shownPath`.

**An entry says whether it is hidden; the panel says whether that matters.**
`EntryVisibility.classify` is a pure function of a name and its
`FileAttributes` — `Hidden` for a leading dot, for `FileAttributes.Hidden` and
for `FileAttributes.System`, `Plain` otherwise — so both conventions are asked
about on both platforms rather than one rule per OS. `read` puts the answer on
every `Entry`, which keeps the listing a statement of what is there;
`HiddenEntries` (`Shown`/`Concealed`) is the panel's setting, and
`DirectoryListing.shown` is the one place the two meet, filtering before `order`
runs. `..` is built with `Plain` regardless of what its directory is marked
with, and it is prepended after the filter anyway, so it can never be concealed.

**Hiding is a reload, where sorting is not.** `sortBy` re-orders entries the
panel already holds, but nothing can display what was never read, so
`AppState.toggleHidden` switches the flag and then runs the same
`loadPanelFocusing` that `Ctrl+R` does — which is why it answers with a
`Result`, reports a `ListingError` through `ReportListingError`, and a directory
that vanished meanwhile leaves the panel where it was. Keeping every entry in
the state and filtering at render time was the alternative and was dropped: the
cursor is an index into what is displayed, so two lists would mean two
meanings for every index.

**The hidden toggle is on `Alt+H`, because `Ctrl+H` is the byte `0x08`.**
`Ctrl+H` is the classic binding and it was the first choice, but pressing it in
the running app did nothing. Feeding synthetic input records to Terminal.Gui's
own `WindowsKeyConverter` says why: a console key event carrying the virtual key
`H` with the control state answers `Ctrl+H`, but a terminal in VT input mode —
which is what Windows Terminal gives — sends only the byte `0x08` for `Ctrl+H`,
and `0x08` *is* backspace, so the converter answers `Backspace` (or
`Ctrl+Backspace`). No layer above it can tell the two apart; the kitty keyboard
protocol exists precisely to make them distinguishable. `Ctrl+R` and
`Ctrl+F3..F6` are unaffected because `0x12` and the function-key sequences
collide with nothing. So `Alt+H` carries the label on the bar and `Ctrl+H` stays
bound but off the bar, the way `Insert` and `Space` share one command — it works
wherever the terminal reports it, and promises nothing where it cannot.

**Concealing an entry drops its mark.** The reload keeps the marks that survive
it, exactly like `refresh`, and an entry that has just been hidden does not
survive. That is deliberate: `F5`, `F6` and `F8` work on the marks, and a mark
nobody can see is a file operated on by accident.

**Which `VisualRole` a view draws with was measured, not guessed.** A view asks
its scheme for an attribute through the `GettingAttributeForRole` event, so
subscribing to it and drawing the view answers the question directly:
`ListView` uses `Normal` for ordinary rows, `Focus` for the row under the cursor
*only while it has focus*, and `Highlight` for marked ones; `FrameView` draws
its border and title with `Normal` whether focused or not; `StatusBar` uses
`Normal` and `HotNormal`. That settled the palette — the cursor bar disappearing
from the panel that lost focus is Terminal.Gui's own behaviour, and it is what
the managers Marbas follows do anyway.

**The session is a value, and the state is built from it.** `Settings` holds
exactly what survives a restart — path, sort order and hidden toggle, per panel
— in the panel's own domain types, so nothing has to be converted twice.
`AppState.fromSettings` builds a state from one and `AppState.settings` reads one
back, which makes `AppState.create path` simply
`fromSettings (Settings.forPath path)`. The first-start defaults therefore live
in one place, `Settings`, rather than in the state constructor.

**Settings go through `IFileSystem`, not through a port of their own.** The plan
called for a load port and a save port; what it got is one `SettingsPath` on
`Dependencies` plus `Settings.load` and `Settings.save` over the file system that
is already injected. `Program.fs` is still the only place naming a real path
(`%APPDATA%\Marbas\settings.conf`), and the parsing, the fallbacks and the file
itself all run against `MockFileSystem` in the tests — which two opaque function
ports would have left untestable.

**The config file is `key=value` lines, and every value is optional.**
`left.path`, `left.sort`, `left.direction`, `left.hidden`, and the same four for
`right`. `Settings.parse` takes the defaults and folds onto them whatever it
understood: an unknown key, a line with no `=`, a blank path and a word that
names no sort key are all simply not read. A truncated or hand-edited file
therefore degrades one setting at a time instead of being rejected whole. Keys
and value words are matched lowercased; a path keeps the case it was written in.

**A stored path is checked before it is used.** `Settings.load` ends by asking
the file system whether each stored path is still a directory and falls back to
the default start path when it is not, so a removed drive cannot greet a fresh
start with an error dialog. Nothing else needs checking: a sort order and a
hidden toggle cannot go stale.

**Saving is an effect, so quitting stays a transition.** `QuitRequested` answers
with two effects — `SaveSettings(settings state)` and then `Quit` — which keeps
*what* is written a pure function of the state, asserted in `UpdateTests`, and
keeps the writing itself in `Shell.interpret` with everything else that touches
the world. A write that fails is swallowed: by then there is no screen left to
report it on.

**`Ctrl+Q` joined the key table, so quitting passes through the state.** It used
to be only an application-level `Command.Quit` binding, which bypasses `Update`
entirely — `F10` would have saved the session and `Ctrl+Q` would have dropped it.
`KeyMap` now owns `quitKey` (off the bar, since `F10 Quit` already carries the
label) and `MainWindow.configureQuitKey` binds the same key application-wide, so
the panel handler wins wherever a panel has focus and the old binding stays as
the fallback everywhere else. It also displaces `Esc`, which is what
`Command.Quit` answers to by default.

**Coverage is a reading, not a target.** P8.1 measured the suite rather than
adding to it blindly: the collector named the lines no test reached, and each
one was either given a test that says something (a read-only file refuses to be
deleted, a file in use cannot be viewed, an unreadable config file falls back to
the defaults, every sort key survives being written and read back, a transfer
whose other panel vanished answers with an error) or written down in
[TESTING.md](TESTING.md) as unreachable with a reason. `Program.fs` is excluded
in `coverlet.runsettings` instead of being counted and apologised for: it is the
composition root, and a test that could reach it would be starting the real
application.

**A failure is arranged on the file itself.** `MockFileData.Attributes` set to
`ReadOnly` and `AllowedFileShare` set to `None` are what make the `try … with`
paths of `Operations`, `FileContent` and `Settings` ordinary tests, so no
hand-written stub of `IFileSystem` exists anywhere in the suite. Directory
enumeration has no such switch, which is why four lines stay uncovered.

**CI runs the same three commands a developer does.** `.github/workflows/ci.yml`
is restore, build, test — no bespoke script, no test-only project settings — so
a green local run means a green remote one. Since P8.2 it runs them twice, on
`windows-latest` and `ubuntu-latest`, with `fail-fast` off so one platform's
failure still shows the other's result.

**One root fixture is what makes one suite serve two platforms.**
`Marbas.Tests.Root.at "start/docs"` answers `C:\start\docs` on Windows and
`/start/docs` on Linux, and every arrange block goes through it — some seven
hundred literals were rewritten at once. `Root.relative` does the same for a
path with no root, which is what "a name carrying a directory is not a bare
name" needs, since a backslash is an ordinary character in a Linux file name and
the test would otherwise assert the opposite of the truth. The alternative —
keeping `C:\…` everywhere and teaching `MockFileSystem` to accept it — does not
exist: the mock normalises through the real `System.IO.Path`.

**Three tests skip off Windows, and say why.** `[<WindowsFact>]` sets `Skip`
unless `OperatingSystem.IsWindows()`. It is used only where drive letters are
the subject, because `MockFileSystem` emulates drives Windows-style and answers a
duplicated `/:\` on Linux — asserting on that would be asserting on a bug in the
mock. Everything else about the root chooser is covered on both platforms
through `RootChoice`.

**The roots a panel can be sent to are a value, not a platform check.**
`Drives.list` used to call `DriveInfo.GetDrives()` everywhere, which on Unix
meant a list of mount points — and no way at all to reach the home directory,
though P3.5 had asked for `/` and `~`. It is now `Roots.list fs choice` with
`RootChoice` being `MountedDrives` or `RootAndHome home`; `Program.fs` picks one
and `Shell.Dependencies` carries it, so both branches are ordinary tested code
and neither platform is a special case below the composition root. The module
was renamed from `Drives` because on Unix it does not answer drives, and the
vocabulary follows: `RootRequested`, `PromptForRoot`, `leftRootKey`, and a
dialog that says *Select root*.

**`SpecialFolder.ApplicationData` is empty on a Linux machine without
`~/.config`.** The old `Program.fs` combined that empty string with `Marbas` and
`settings.conf`, which yields a *relative* path — so the session was written to
`./Marbas/settings.conf`, next to wherever the app was started, and a start from
another directory found no session at all. Running the app under WSL is what
caught it; no test could have, because the value comes from the environment.
`Settings.location` now holds the rule — application data if the platform names
one, `$HOME/.config` otherwise — and it takes both values as arguments, so it is
tested on both platforms while `Program.fs` keeps only the two environment
reads.

**A release is one file, and it carries its own runtime.** The two publish
profiles are self-contained and single-file, with the native libraries and the
compression inside, so what ships is one executable of about 50 MB that runs
where no .NET is installed — a file manager is a tool you copy onto a machine,
not something you install a runtime for. Trimming is off on purpose:
Terminal.Gui resolves drivers and views by reflection and FSharp.Core formats
and compares by it, neither of which a trimmer can see, so a trimmed build is
one that starts and then fails on the path nobody tried. Globalization stays in
for the same reason in miniature — entry names are sorted culture-aware, and
`InvariantGlobalization` would change the order a panel lists its files in.
Details and the rest of the properties are in [PACKAGING.md](PACKAGING.md).

**The tag is the version.** `Marbas.fsproj` carries a `Version` for an ordinary
`dotnet publish`, and `release.yml` overrides it from the tag it was triggered
by, so the version stamped on the binary and the version of the release cannot
drift apart. Each profile is published on its own runner — Windows on
`windows-latest`, Linux on `ubuntu-latest` — even though either could be
cross-published from the other, so what is released is what that platform's SDK
produced. The archives differ for one reason: `.tar.gz` on Linux keeps the
executable bit, which a `.zip` drops.

**A published binary is checked by running it, not by a green publish.** Nothing
in `dotnet test` touches the build itself, and a self-contained build fails at
startup if it fails at all. The Linux binary was run under WSL through the same
pseudo-terminal recipe P8.2 used and quit with `F10`, which wrote the session
file; the Windows one was started from its publish directory and outlived the
wait. What either *looks* like stays a manual row in
[CROSS-PLATFORM.md](CROSS-PLATFORM.md).

**A key that must not leave the panel is one the panel marks handled.**
`ListView.MoveDown` answers `false` at the last entry, and an unhandled key
walks up to Terminal.Gui's focus navigation — which is why the cursor used to
land in the other panel. `PanelWiring` therefore ends in
`key.Handled <- handle …` and `stopsAtEdge` answers `true` for `↑`/`PgUp`/`Home`
at the top and `↓`/`PgDn`/`End` at the bottom: the panel swallows the key and
nothing happens, which is what the orthodox managers do. `←` and `→` were never
bound in `ListView` at all, so they escaped immediately; they are now handled
outright.

**A page jump is a pure function of the page on screen.** `PageJump.forward`
and `PageJump.backward` take `{ FirstVisible; Height; Count }` and a cursor and
answer an index — the far edge of the visible page if the cursor is not on it
yet, otherwise a whole page further. The geometry comes from `ListView`'s own
`Viewport.Y` and `Viewport.Height`, which is the list's scroll offset and the
rows it shows, so a scrolled panel jumps from where it actually stands. The rule
lives in `Ui` because only `Ui` knows the geometry; what it produces is an
ordinary `CursorMoved`, so no new message carries a cursor.

**An entry says whether it is a program; `Ui` says what colour that is.**
`Executability.classify` mirrors `EntryVisibility.classify`: it takes the name
and the `UnixFileMode` and answers `Executable` for a Windows program extension
*or* for any execute bit, so both platforms' conventions are asked about on both.
`IFileSystemInfo.UnixFileMode` answers `None` on Windows rather than throwing,
and `MockFileSystem` lets a test set it, which is what makes the Unix half
testable from a Windows machine. Which colour an entry then gets is
`EntryColor.classify` — directory, archive (by extension), executable, plain —
and it is `Ui`'s alone, because an archive is a display grouping and not a fact
about the file.

**Rows are painted through `RowRender`, and only the ones nobody else owns.**
`ListViewRowEventArgs.RowAttribute` overrides whatever attribute `ListView`
chose for that row, marked and focused included, so the handler must hand the
row back when it is marked (`Source.IsMarked`) or under the cursor of a focused
list. What it needs is one attribute per row, which is why `FilePanel.PanelView`
is a `FrameView` carrying a `RowColors` array that `render` fills — the handler
is attached once and reads the array, rather than being re-subscribed on every
render. `OnRowRender` is public, so a test raises it and reads the attribute
back; nothing else about colour is observable without a driver.

**The bottom row of a panel has three states, and one module picks.**
`PanelFooter.describe` takes the whole `PanelState`: an open search wins, then
the marks, then what the directory holds. A blank row read as the end of the
listing, which is why it is never blank now and why a `Line` separates it from
the entries. `SelectionSummary` kept its job — it describes marked entries — and
`PanelFooter` is the choice above it.

**A search is a domain type, and the panel it belongs to holds it.**
`SearchText` sits beside `NamePattern`: a single-case union over a string with
`ofChar`, `append`, `backspace` (which answers `None` on the last character, so
running out of text *is* closing the search) and `matches`, which is a
case-insensitive substring test. `PanelState.Search` makes it per panel, so the
footer of the panel being searched is the one that changes, and
`loadPanelFocusing` clears it along with the marks — a search cannot outlive the
listing it was run against. `activate` clears both panels' searches, which is
what closes one when `Tab` moves on.

**Which mode the keyboard is in decides who reads a key first.** With no search
open, `KeyMap` wins and a printable key left over opens a search; with one open,
every printable key is text — `Space` and `+` included — and anything else
closes the search before being handled normally. `PanelWiring.Host` grew an
`IsSearching` for exactly that question, answered from `Runtime.State`. The
alternative, letting `KeyMap` always win, would have made a file name with a
space unsearchable.

## Traps already hit (details in [TESTING.md](TESTING.md))

- `StatusBar` shortcuts swallow their key even with a `null` action — `F10`
  looked bound but did nothing until it got a real callback.
- `Key` is mutable: sending a shared instance sets `Handled` on it forever, so
  later sends are silently ignored. Tests send fresh copies via
  `Gui.press`.
- Keys do not bubble from a nested view to its parent, so handlers live on the
  focused `ListView`.
- `View.AddCommand` is `protected`; custom shortcuts use `KeyDown` handlers.
- `ListView` type-ahead search swallowed `+` and `-` before any handler saw
  them; the panel disables it with `KeystrokeNavigator <- null`. It stays
  disabled now that letters *do* move the cursor, because its own matching is
  prefix-only, invisible and unaware of the panel state — the search Marbas
  runs is its own.
- A key `ListView` cannot act on is reported unhandled and walks up to focus
  navigation, so `↓` at the last entry moved to the other panel. Nothing in the
  key table said so; only pressing the key does.
- A key binding no test can vouch for: tests send keys straight to the view, so
  they prove the handler, never the terminal. `Ctrl+H` passed every test and did
  nothing in the running app, because a VT terminal sends `0x08` for it — the
  byte backspace already owns. Feeding a synthetic `InputRecord` to
  Terminal.Gui's internal `WindowsKeyConverter` from `dotnet fsi` is what settled
  it, and is the way to check the next binding that "should" work.
- `Environment.GetFolderPath SpecialFolder.ApplicationData` answers an empty
  string on Linux when `~/.config` does not exist yet, and combining an empty
  string into a path yields a *relative* one that quietly resolves against the
  working directory. `Settings.save` swallows its failures, so nothing complained
  — the session simply appeared in `./Marbas/settings.conf` and was never found
  again. Only running the application caught it.
- `MockFileSystem` emulates drives Windows-style on every platform: on Linux
  `DriveInfo.GetDrives()` answers `/:\` twice. Any test that asserts on the drive
  list is therefore Windows-only, whatever the code under test does.
- The stock Visual Studio `.gitignore` ignores `*.pubxml`, because a *web*
  deploy profile can hold a connection string with a password in it. A publish
  profile for a console application holds nothing of the sort, so both files
  would have been silently left out of the commit; `.gitignore` now un-ignores
  `Marbas/Properties/PublishProfiles/*.pubxml`. `git status` is what says so —
  a new file that does not appear there is ignored, not missed.
- An F# list literal that mixes plain elements with a `for … -> …` comprehension
  keeps only what the comprehension yields; the plain elements become ignored
  statements. `KeyMap.bindings` lost every function key that way and only the
  two selection keys survived, which looked like a broken key handler. The
  compiler does say so (`FS0020`) — the warning was there to be read.

## Next up

**The plan is finished.** Every task from P1.1 to P8.3 is done, so what follows
is no longer plan order but choice. Three things are waiting:

- The rows still marked *manual* in [CROSS-PLATFORM.md](CROSS-PLATFORM.md) —
  colours, a resized terminal, the mouse, a real Unix permission bit, and `F4`
  with a full-screen console editor — want a person at a terminal on each
  platform, and now also the published binaries rather than `dotnet run`.
- The one known defect: a full-screen console editor on Unix shares the terminal
  with a driver that never let go of it, which needs a driver shutdown around
  the child process and a switch to keep the detour off Windows.
- Everything under *Known rough edges* below, of which both panels starting on
  the same directory is the one a user meets first.

## Known rough edges

- The entry colours and the rule under the listing are drawn by a driver no test
  has, so what a test can say is which attribute `RowRender` hands back and
  where the `Line` sits — not what the screen looks like. Both belong on the
  manual row of [CROSS-PLATFORM.md](CROSS-PLATFORM.md), and bright blue on the
  panel's blue background is the one to look at first.
- The search matches the first entry from the top of the listing and there is no
  "next match" key, so a name that shares its text with an earlier one cannot be
  reached by typing alone. Searching on from the cursor, or binding a repeat
  key, is the fix whenever it starts to bother.
- `Home` and `End` are swallowed at the ends of the listing rather than handled,
  which is invisible while nothing else wants them.
- Both panels start on the same directory, so `F5` straight after launch copies
  a file onto itself and the OS refuses it with an error dialog. `F6` can be
  pointed elsewhere by hand, `F5` cannot — a target prompt for `F5`, or a check
  that the two directories differ, would be the fix.
- Progress is posted per file through `app.Invoke`. A tree of thousands of tiny
  files therefore queues thousands of callbacks; if that ever stutters, the fix
  is to coalesce (keep the latest path in a field and refresh on a timer)
  rather than to report less.
- The viewer is opened by `Shell` through the `ShowFile` port, so the modal
  run itself (`app.Run dialog` in `Program.fs`) is only exercised by running
  the app — the same blind spot the progress dialog has. What the tests do
  cover is that the viewer takes `F3`/`Esc` for itself before anything else
  sees them.
- `F4` starts the editor as a child process sharing this terminal and only
  forces a redraw (`ClearScreenNextIteration`) when it returns. A windowed
  editor — `notepad`, VS Code — round-trips cleanly. A full-screen console
  editor on Unix shares the terminal with a Terminal.Gui driver that is still
  reading input, and there is no suspend/resume in the v2 API for a child
  process (`IDriver.Suspend` is `SIGTSTP`, a different thing). If that turns
  out to matter, the fix is to shut the driver down around the process and
  re-initialise afterwards, and it touches `Program.fs` only.
- The cursor is invisible while it stands on a marked entry: `ListView` asks for
  `Focus` and then for `Highlight` on that row, so the marked colour is the one
  that lands. Colouring it the classic way — the marked yellow on the cursor's
  cyan — needs the `RowRender` event, which hands over the row number and takes
  a `RowAttribute` that overrides the role. It is one handler in `FilePanel`
  whenever it starts to bother.
- Dialogs, message boxes and the `F3` viewer keep Terminal.Gui's own theme; only
  the main screen wears the classic colours. They are five separate surfaces
  (`PromptDialog`, `PatternDialog`, `ProgressDialog`, `FileViewer`, `MessageBox`)
  and colouring one without the rest would look worse than colouring none.
- The modifier row only appears on Windows. `WatchModifiers` registers nothing
  elsewhere, because no Unix driver in Terminal.Gui 2.4.17 reports a standalone
  modifier outside the kitty keyboard protocol. If that protocol is ever wired
  through, the fix is one more implementation of the same port.
- The poll reads the physical keyboard, not this window's: holding `Ctrl` in
  another application changes Marbas's bar too, if it happens to be visible.
  `GetConsoleWindow` would gate it, but under Windows Terminal that handle is
  not the window the user sees, so the gate would switch the feature off where
  it matters most.
- The bar follows the keyboard ten times a second, so a `Ctrl` tapped and
  released inside 100 ms may never show its row. Polling faster costs more than
  the miss does.
- `Ctrl+H` toggles hidden entries only where the terminal reports it as a key
  rather than as the byte `0x08`, which no terminal outside the kitty keyboard
  protocol does. Under Windows Terminal it arrives as `Backspace` and does
  nothing, which is why `Alt+H` is the binding on the bar. Nothing is bound to
  `Backspace` itself, so the collision is silent rather than surprising — but it
  is also the reason `Backspace` cannot later be given the "go up one directory"
  meaning some managers have without a `Ctrl+H` pressed elsewhere reaching it.
- Delete still runs on the UI thread inside an `AppState` transition, so
  removing a huge tree blocks. It was never routed through an effect the way
  copy and move are, and `Directory.Delete` cannot report progress anyway.
- The session is written only on quit, so a crash or a closed terminal loses it.
  Writing after every navigation would keep it current at the cost of a file
  write per keystroke; a write on a timer is the middle road if it ever matters.
- `Ctrl+Q` pressed while a dialog holds focus still quits through the
  application binding, which does not go through `Update` and therefore does not
  save. Unbinding `Command.Quit` would fix it and hand `Esc` back its default
  quit meaning, which is worse.
- Which panel is active, where the cursors stand and what is marked are not
  saved — P7.4 asked for paths, sort orders and toggles. The cursor would be the
  obvious next one, and it fits the file as `left.cursor=<name>`, resolved the
  way `FocusTarget.ByName` already resolves.
- The settings file is read and written through the same `IFileSystem` the
  panels browse. That is what makes it testable, but it also means a future
  read-only or sandboxed file system would silently take the config with it.
