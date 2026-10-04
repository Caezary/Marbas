# State and effects

**Immutable state, one mutable cell.** `AppState` transitions answer a new state
from the old one; `Update.update` folds a `Message` into that state plus a list
of `Effect` values, and the generic `Runtime` holds the only mutable reference,
re-renders after each dispatch and hands the effects to the interpreter.
Handlers send messages, they never mutate. See PLAN.md, *State handling*.

**A transition may read and write through `IFileSystem`, and through nothing
else.** That is the boundary, and it is drawn by kind rather than by cost: a
transition takes the `IFileSystem` it was given and may list a directory, read
an archive's index, create a directory or write an entry's properties with it,
which is what makes every one of them testable against `MockFileSystem` alone.
Every other port — a dialog, the trash, a process, a link, the search runner,
the batch runner, quitting — belongs to `Ports.Dependencies`, and the state
layer reaches it only by raising an `Effect` that `Shell.interpret` carries out.

**A panel is changed on its own; the state only says which one.** Everything
that moves a cursor, marks, searches, sorts or reads the marks back without
touching the disk is in the `PanelState` module (`State/PanelState.fs`), a
function from one `PanelState` to the next with tests of its own that build a
panel by hand and need no file system. `AppState` holds the rest: the loaders
that read a location into a panel, the `finish*` transitions that reload after
a batch, the queries `Update` asks before raising an effect — `transferSource`,
`operationPaths`, `cursorFilePath`, `measureTargets`, `deleteQuestion` — and
the tabs. What it keeps of the panel functions is one line each that says which
side they apply to, `setCursor side index = updatePanel side
(PanelState.withCursor index)`, so a test or a message that addresses a side
still reads as one call. The split was P15.22's answer to A11; `FocusTarget`
moved with the panel type because `PanelState.cursorFocus` answers one.
The test for which side a query belongs to is whether it *uses* `fs`, not
whether it declares it: `cursorEntryInArchive`, `cursorFileInArchive` and
`targetsDirectoryInArchive` carried an `fs` they only handed on, stayed behind
in the first pass for it, and moved after it. The place of a name inside an
archive, which they and `transferSource` both need, is not a panel's at all —
it is `PanelLocation.archivePlace` and `FindHit.archivePlace` in the
file-system layer.

**Work that outlasts a keystroke is an effect too.** Copy, move, delete, wipe,
pack, unpack, measure, a rename that fell back to copying and a search all run
on a background task under a progress window that `Esc` stops, because a
transition holds the UI thread for as long as it runs. Anything a transition
does with `IFileSystem` is therefore bounded by what is on screen — one
directory, one archive level, one entry — and what scales with the data behind
it is asked for by an effect that answers with a message. `DeleteRequested` is
the smallest example: it raises `InspectDelete` carrying the paths whose
contents the warning depends on, the interpreter asks the file system about
them, and `DeleteScopeLoaded` brings the `DeleteScope` back for the
confirmation to be built from.

**`Dispatch` enqueues; one loop drains.** Since P15.8 a message is put on a
queue and, unless a drain is already running, one loop takes messages off it and
runs update → render → interpret until the queue is empty. `Shell.interpret`
dispatches from inside several effects, so before the queue such a message ran
to completion — its own effects included — in the middle of the outer effect
list. Two things follow from the loop. An effect is always interpreted against
the state it was raised from, so the `WatchLocations` that `Update.watching`
appends can no longer be overtaken by a nested one and then re-armed on the
older directories. And a batch that finishes inside `deps.Runners.Batch`, which
dispatches from within the dialog's own event loop, no longer re-enters the
runtime several dispatches deep. The drain is still synchronous: `Dispatch`
returns with the queue empty, which is what lets `MainWindow.create` read
`Runtime.State` straight after seeding the panels.

**The outside world is injected.** `Ports.Dependencies` names every port and
groups them by what they do to the world: `Prompts` asks the person something
(errors, confirmations, names, patterns, passwords, the find and link popups),
`Processes` reaches the operating system (trash, editor, launch, links,
handlers), `Clipboard` hands things to other programs, `Watches` subscribes to
the system and `Runners` puts work on a background task. `Shell.interpret` is
the only place that turns an `Effect` into a call on one of them. **Every prompt has a type of its own:** each takes a request record
(`ErrorReport`, `Confirmation`, `NamePrompt`, `TargetPrompt`, …) and answers a
domain value, so wiring the target prompt where the name prompt was meant no
longer compiles, and the two that used to share `string -> string -> string
option` now cannot be swapped. Where a prompt answers a domain type, the parse
lives with the dialog that read the text — `PromptDialog.name` answers an
`EntryName`, `PromptDialog.password` an `ArchivePassword`, `PatternDialog.pattern`
a `NamePattern` — which keeps it inside the tested `Ui` layer instead of the
adapter.
`Adapters/Prompts.fs` is the only place that opens real dialogs, which keeps
dialog-triggering behaviour testable. The dialogs themselves are built in `Ui`
modules (`PatternDialog`) so their layout can be tested too — a field placed
outside the dialog's content area is invisible yet still typable, which no
behaviour test would notice.

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

**The cursor lives in the state, and rendering is guarded.** The panel's
`ListView.ValueChanged` dispatches `AppState.setCursor`, so state always knows
where the cursor is. Rendering also writes `ListView.SelectedItem`, which
raises the same event, so `Runtime` raises its `IsRendering` flag while it
renders and the handler ignores events raised inside it. Without the flag,
render and handler bounce off each other.

**`MainWindow` is a composition root, `AppView` is the screen.** `AppView.create`
builds the window, the two panels and the status line and hands back a record;
`AppView.render` is the whole screen as a function of `AppState`, so it can be
tested by rendering a state into a view and reading the panels back, with no
`Runtime` involved. Each panel remembers what it last painted and skips the
work a message did not change, which
[UI-AND-INPUT.md](UI-AND-INPUT.md) describes; the memo is a cache in front of
that function, not a second notion of what the screen shows. The side-to-view pairing lives in `FilePanel.Panels` and
`FilePanel.pairs`, which is what render, seeding and wiring all iterate.
`MainWindow.create` is left with four steps: build the view, build the runtime,
seed both panels, attach the wiring. The function key bar is added after the
runtime exists, because it needs `dispatch` — which needs the runtime, which
needs the render function, which needs the views.

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

**Which mode the keyboard is in decides who reads a key first.** With no search
open, `KeyMap` wins and a printable key left over opens a search; with one open,
every printable key is text — `Space` and `+` included — and anything else
closes the search before being handled normally. `PanelWiring.Host` grew an
`IsSearching` for exactly that question, answered from `Runtime.State`. The
alternative, letting `KeyMap` always win, would have made a file name with a
space unsearchable.

**The clipboard is a port, filled on one system.** `Ports.Clipboard` holds
`SetText`, which takes a `ClipboardText` and answers
`Result<unit, ClipboardError>`, and `GetText`, which answers the text the
clipboard holds or `None` when it holds no text; the list of files joins them
with the tasks of phase 18 that need it. What `GetText` answers is a `string`
on purpose: it is text not yet parsed, and `PastedPath.read` is what makes a
path of it. `ClipboardText` is a list of lines that
cannot be empty, and the line break is not in it: `ClipboardText.joined` takes
the break from whoever spells the text, so the state that builds it stays the
same on every system and the adapter adds the one its system uses.
`Adapters/SystemClipboard` speaks the Win32 clipboard on Windows — open it (a
few short retries, since another program may be holding it), empty it, hand
over a `CF_UNICODETEXT` block, or ask whether one is there and copy it out —
and answers `ClipboardUnavailable` on Unix without trying, for reading and
writing alike. A refusal either way comes back to the state as
`ClipboardRefused` (P18.1 named it `ClipboardUnwritten`, before the clipboard
could be read), and the state decides how loud it is: `ClipboardUnavailable` is reported the first time and
then passed over, through `AppState.ClipboardReport`, the same
`FailureReport` that keeps the unwritable history to one dialog; a
`ClipboardFailed` carries the system's reason and is reported every time,
because a clipboard another program held a moment ago may well be free on the
next press.

**The environment is read through a port.** `Processes.EnvironmentVariable`
answers the value of a variable, or nothing when it is unset or empty;
`HostEnvironment.variable` fills it. `PastedPath.read` takes it as a function,
which is what lets a test expand `%DATA%`, `$DATA` and `~` against a map
instead of against the machine running the suite.

**The background thread is a port, not a thread.** `Runners.Batch`
takes the progress title, the work to do (handed a `BatchControl` to report
through and to ask whether it should stop) and what to do with the results.
`BatchRunner` is the only place that knows about `Task.Run`, `app.Invoke` and
running the progress dialog modally; the tests pass a runner that does the work
inline, which is why every `F5`/`F6` test stayed synchronous and none of them
had to change when the operations moved off the UI thread.

**Cancelling means "the progress window is gone".** The token is cancelled the
moment `app.Run dialog` returns, whatever the reason. `Esc` closes the dialog,
so the batch stops at its next file; if instead the batch finished first, its
`Invoke` callback closes the dialog and the cancel that follows changes
nothing. The same flag guards the stop request, so a batch that finishes after
the window is already gone cannot stop the main window by mistake — which
would quit the application. A progress report is checked twice: once on the
worker, so nothing is queued after a cancel, and again when the UI thread runs
it, because a report queued just before `Esc` is still waiting in the loop
after the dialog has been disposed. Without that second look it asked the
disposed dialog for its line, found none, and the exception took the
application down.

**A stopped entry is reported, not passed over in silence.** Cancelling inside
a tree leaves it half-copied, so the item answers `Cancelled path`, counts as a
failure and is listed with the others; its mark stays, because it never
finished. Entries the batch never reached are simply absent. A move that was
interrupted never deletes its source: the delete is sequenced after the copy in
the same `result` block, so a failed copy skips it.

**`ReportError` carries a title.** Listing failures and operation failures want
different dialog captions, so the dependency is `title -> message -> unit`;
`ListingErrorMessage`/`OperationErrorMessage` each own their `Title`, and
`Prompts.reportError` just forwards both to the message box.

**Operation errors are mapped, not re-thrown, inside a transition.**
`AppState.makeDirectory` creates the directory and then reloads the panel; the
reload returns a `ListingError`, which a private pure mapping folds into an
`OperationError` so the whole transition answers with one error type. State
still never depends on Ui — the mapping is over the raw error values.

**`F4` reads the entry under the cursor and answers with a different effect for
each kind.** `EditRequested` is one message; `Update` asks the panel whether the
cursor stands on a file or on a directory and raises `EditFile` or
`ShowProperties`. `..` answers neither, so it still starts nothing. That keeps
the key binding one line in `KeyMap` and the decision where the state is, which
is also the only place that knows which editor an extension is associated with:
`EditFile` carries the resolved `ProgramPath option` beside the path, so
`Shell` and the adapters never look a setting up.

**Reading and writing properties happen inside a transition, like making a
directory.** `EditRequested` reads them (`EntryProperties.read`, mapped to
`ReportOperationError` when the entry is gone) and `PropertiesEntered` writes
them and re-reads the panel, so a directory that has just been marked hidden
disappears from a panel that conceals hidden entries without anyone asking for a
refresh. The popup itself is a port — `Prompts.EditProperties` takes the
properties it should show and answers with the ones the user accepted, or
`None` — which is what lets every one of those paths be tested with no dialog on
screen.

**`Shift+F4` creates the file first and edits it second, in one transition.**
`FileNameEntered` runs `AppState.makeFile`, which creates the file, reloads the
panel with the cursor on it and hands the path back; the resulting state is what
`EditFile` is raised from, so the editor is chosen from the name that was just
typed. A name a file already answers to is opened as it stands rather than
truncated — the point of the key is to reach an editor, not to erase anything —
while a name a *directory* answers to is a `TargetExists` failure.

**`Enter` is one message with four answers, and the order is the whole rule.**
`EntryOpened` asks, in this order: is the cursor on a file inside an archive
(refuse, `ArchiveEntryNotRunnable`); is it on a file on disk whose extension
carries a `ForOpening` row (run that program); is that file a readable archive
(walk into it); otherwise hand it to the system. Everything that is not a file
on disk — a directory, `..`, a directory inside an archive — falls through to
`AppState.enter`, which is exactly what it always did. The order matters twice
over: the association beats the archive walk, which is why `WalkIntoRequested`
exists on `Alt+W` and `Ctrl+PgDn`, and the in-archive refusal comes first so
that a `.zip` inside a `.zip` is refused rather than run out of nowhere.

**The open-with key asks the same question of a different thing at every place
the cursor can stand.** `OpenWithRequested` refuses outright when the panel is
*inside an archive* — not only on a file, the way `Enter` does, because there is
nothing there to open with anything: a directory inside an archive is a level,
not a place the desktop can be sent to. On disk it raises `ShowHandlers` for a
file, carrying the `ForOpening` row the extension already has so the popup can
open on it, and for a directory it raises `RunProgram` with the *directory* as
the target and no program — which is how the system's own file manager is
reached, since that is what shell-executing a directory and `xdg-open` both do.
`..` is the directory the panel *stands in*, not the one above it: the cursor
sitting on the way out is not a request to reveal somewhere else, it is the
resting place of a cursor in a panel someone wants opened. Both are resolved
before the effect, so the popup and the launcher never have to know about `..`
at all.

**Remembering happens in the transition, not in the popup.** `HandlerChosen`
carries the file and an `OpenWithChoice` — a program and a `HandlerUse`,
`AlwaysThisWay` or `ThisTimeOnly`, rather than the `bool` it was until P15.21 —
and `Update` writes the `ForOpening` row into `state.Programs` before raising
`RunProgram`. The popup never touches the table, which is what lets the same
`ProgramSettings.associate` serve `F12` and the open-with key, and what puts the
remembered choice into the file the session is saved to without a second path
through the settings.

**The elevated run is `Enter`'s run with one field changed, and it reads no
table.** `ElevatedRunRequested` raises the same `RunProgram` effect with
`Elevation = AsAdministrator` and no program at all: what is being raised is the
rights of the entry under the cursor, so a `ForOpening` row — which would make
`Enter` start something else entirely — has no business here. Only
`cursorExecutablePath` answers it. Anything else on disk that has a path, a
document or a directory, becomes `ReportLaunchError(NotAProgram …)`, which is a
`LaunchError` no adapter ever returns: refusing before the port is the same kind
of answer as refusing inside it, and putting it in the same type means it is
worded in the same place. `..` has no path of its own to run and raises nothing.
A panel *inside an archive* refuses exactly as the open-with key does, through
the one `reportNotRunnable` both share, and the `ElevationDeclined` a real
prompt turns down was already silence rather than a dialog.

**The cursor is what runs, not the marks.** `EntryOpened` reads
`cursorFilePath` and `cursorFileInArchive`, never `operationTargets`. Every
other command in the app works on the marked entries and falls back to the
cursor; `Enter` is the one that does not, because "run these six files" is not
what pressing it means anywhere else in the world either.

**Starting a program is an effect that changes no state.** `RunProgram` carries
a `LaunchRequest` and `LaunchFinished` brings the answer back, and the
transition for it does nothing to the panels either way: unlike `EditFinished`,
which re-reads the directory the editor was pointed into, nothing here says when
the program is done — or that it ever will be. A failure becomes
`ReportLaunchError`, except `ElevationDeclined`, which becomes nothing at all.
`EditFinished` answers `Operations.StartDeclined` the same way, so a prompt the
user turns down is silence whichever key started the program: `mapException`
turns the Windows code for it into that value before the transition ever sees
it, the way it turns the code for "nothing is associated" into
`NoProgramAssociated` rather than into a sentence the system wrote.

**The settings popup is a port too, and what it returns is state.**
`SettingsRequested` hands `state.Programs` to `Prompts.EditPrograms` through
the `ShowSettings` effect, along with the executable under the panel cursor if
there is one — the popup has no panel to look at, so what it can offer has to be
read before it opens; `ProgramsEntered` puts the answer back into the state
and nothing else. It reaches the file the way every other setting does, when
`QuitRequested` saves the session.

**Where a panel stands is a location, not a path.** `PanelState.Path` is a
`PanelLocation` — a directory on disk or a level inside an archive — so a
transition that needs a `FileSystemPath` has to ask for one and answer for the
case where there is none. `AppState` asks through `PanelLocation.directory` and
`PanelLocation.combine`: `operationPaths` is empty inside an archive, the
cursor paths are `None`, a reload leaves the panel as it stands, and
`makeDirectory` and `makeFile` take the directory as an argument, because by
then `Update` has already asked whether there is one. `Update` does the asking
before it raises an effect rather than after — `activeDirectory` and
`targetDirectory` gate the dialogs of `F6`, `F7`, `Shift+F1` and `Shift+F4` and
the target of `F5` and `Shift+F2` — so no popup opens on a question that cannot
be answered. See [ARCHIVES.md](ARCHIVES.md), *Where a panel stands*.

**A panel is loaded from a location, and `Enter` is where the two kinds of
place meet.** `AppState.loadLocationFocusing` is the one loader — it reads a
`PanelLocation` and fills the panel from whichever listing that location asks
for — while `loadPanel` stays the disk-only convenience that `PathOpened` and
the drive chooser use, since a root and a start path are always directories.
`enter` matches on the panel's location first and the cursor entry second, so
walking into a directory, into an archive, out of a level and out of an archive
are four arms of one transition rather than four messages. `F3` is the one key
whose effect changed shape: `ViewFile` carries a path on disk, `ViewArchiveEntry`
carries the archive and the `ArchivePath` inside it, and `Shell` opens each
through its own reader — the state never holds a stream. See
[ARCHIVES.md](ARCHIVES.md), *Walking into an archive*.

**What `F5` transfers is a value, not a place the effect has to guess.**
`AppState.transferSource` answers a `TransferSource` — `FromDisk` of paths,
`FromArchive` of an archive and the `ArchivePath`s inside it, or `FromPlaces` of
several such groups — so `Update` raises one `CopyPlaces` whatever the source is
and never asks a second time what kind of place the panel is standing in. The type is
what `F6` will match on when moving out of an archive joins it in P9.13. See
[ARCHIVES.md](ARCHIVES.md), *Copying out of an archive*.

**Where a panel stands decides which key does what.** `AppState.transferSource`
and `AppState.transferTarget` answer what the operation targets are and where
they are going — `FromDisk`/`FromArchive`, `ToDirectory`/`ToArchive` — and
`Update` matches the pair, so `F5`, `F6`, `F7` and `F8` stay one message each
while gaining four destinations between them. A key that would change an
archive of a kind that cannot be changed answers
`ArchiveFailed(KindCannotBeChanged …)` before it raises any other effect, so no
prompt opens and no batch starts; whether the archive is protected by a
password is a question only the file can answer, and `Shell` asks it before it
starts the batch. `F7` inside an archive stays a transition rather than an
effect, because adding one directory entry is as quick as making one on disk.
See [ARCHIVES.md](ARCHIVES.md), *Changing an archive*.

**The state holds tabs, and the tab in front is what the outside sees.**
`AppState.Tabs` is a `Zipper<Tab>` and `Left`, `Right` and `Active` are members
reading the current tab, so `Update` never mentions tabs to move a cursor or
sort a panel — `panel`, `updatePanel` and `activate` funnel through
`Zipper.update` and every transition written before P11.1 kept working
unchanged. What the zipper buys is the invariant: there is always exactly one
current tab, and closing the only one is a key that does nothing rather than a
guard someone has to remember. See [Tabs](TABS.md).

**A tab command is a transition plus a save.** `TabOpened`, `TabClosed`,
`TabSwitched`, `TabCycled` and `SessionSaveRequested` all end in
`SaveSettings(settings fs state)` — the effect `QuitRequested` has always
raised — so what is written stays a pure function of the state and the writing
stays in `Shell.interpret`. `TabSwitched` and `TabCycled` do one thing more:
`AppState.readTabAgain` re-reads both panels of the tab they land on, since a
tab that has been in the background may be standing in a directory that is
gone. A listing that fails there does not undo the switch — the state is the
tab that was asked for, and the effects are the save *and* a
`ReportListingError` beside it.

**The tab popup is a port like every other dialog.** `ShowTabs` carries the
`Zipper<Tab>` — the rows and which one is current in one value — and
`Prompts.ChooseTab` answers with a position or with nothing, which
`Shell.interpret` turns into `TabSwitched`. `Adapters/Prompts.chooseTab` is the
only place that builds the dialog, and it needs the file system for one reason:
`TabRow.describe` spells a `PanelLocation` with `PanelLocation.value`.

**Rendering asks the state how many tabs there are, and the right panel pays for
the answer.** `AppView.render` draws the counter first, then hands each panel
the number of columns the counter took — zero on the left, the counter's width
on the right — so `FilePanel.render` can work out how much room the title has
before `PanelTitle.describe` shortens the path into it. The counter is a label
added to the window after the panels, which is what puts it over the right
panel's top border rather than beside it.

**A watch is an effect, and the state never knows what a watcher is.**
`ExternalChangeDetected` re-reads both panels of the tab in front and raises
nothing, because a change nobody asked for must not answer with a dialog;
`WatchLocations` carries the paths that should be under watch, and
`Ports.Watches.Locations` is the port `Adapters/DirectoryWatch` fills.
What is unusual is how the effect is raised: `update` is the former body, now
`transition`, followed by `watching`, which compares `AppState.watchedLocations`
before and after the message and appends the effect only when the pair moved.
Raising it arm by arm would have meant seven places to keep in step — and
`EntryOpened` alone moves a panel four ways — while the comparison also keeps
every other transition silent. The first watch of a session is not a transition
at all: at startup the panels already stand where the settings put them, so
`MainWindow` arms it by interpreting one `WatchLocations` itself, next to the
`Watches.Modifiers` it already wires. See [Auto-refresh](AUTO-REFRESH.md).
`remembering` follows `watching` the same way: it compares where each panel of
the tab in front stood before and after the message and appends a history
change for a panel that landed somewhere new — see [History](HISTORY.md).

**A search reaches the state once, finished.** `FindEntered` raises `RunSearch`
with the query, the location the panel returns to and its marked directories;
`Shell` resolves the ground and hands both to the `Runners.Search` port, and while the
walk runs its hits live in the results window, not in `AppState`. The window
answers a `FindAnswer` when it closes, which comes back as `SearchAnswered`: the
`FindResults` in it become `AppState.LastSearch`, and the `FindLanding` beside
them — a hit, a directory, the panel, or nothing — decides where the active
panel goes. `LastSearch` belongs to the session rather than to a tab and is
never written to the settings file; `LastResultsRequested` drops what is gone
from it and raises `ShowLastResults`, which fills the same window from the kept
answer. Why the hits are kept out of the state while they arrive is in
[Finding files](FINDING-FILES.md), *The results window*.

**A panel can stand on search results, and every question about a place has a
third answer.** `PanelLocation` has `SearchResults` beside `OnDisk` and
`InArchive`, holding the query, the ground, the hits, the location stood on
before (`Origin`) and the entry the cursor was on there (`Returning`).
`PanelLocation.origin` is where such a panel really is for everything that needs
a place — the session file, a new tab, the next search, the root of its volume —
and `PanelLocation.watched` answers nothing for it, so it drops out of the watch
by the same comparison that raises `WatchLocations`. The paths of its entries
come from the hits rather than from the panel, through `SearchListing.hitFor`,
which is why `operationPaths`, the cursor paths and `enter` each grew a
`SearchResults` arm rather than a second function. A key that needs a directory
where a results panel stands answers `NotInSearchResults` with the
`ResultsRefusal` naming it. `Update.refusedAsSource` and `refusedAsTarget` read
that refusal off the message alone, and `resultsRefusal` weighs them against the
two panels once, in `update`, ahead of the transition: the active panel first,
then the panel that would receive.

**Marks in several places are a `TransferSource` of its own.** `FromPlaces`
holds one `FromDisk` or `FromArchive` group per place, and `Update` maps it onto
`CopyPlaces`, `MovePlaces` and `DeletePlaces`, which `Shell` runs as one batch of
pieces — each built through the effect its place already had. A results panel
whose marks lie in one place answers that place's own case, so nothing about
one place changed. See [Finding files](FINDING-FILES.md), *Operations on the
results*.
