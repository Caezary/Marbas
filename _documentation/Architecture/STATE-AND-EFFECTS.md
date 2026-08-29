# State and effects

**Immutable state, one mutable cell.** `AppState` transitions are pure
functions; `Update.update` folds a `Message` into a new state plus a list of
`Effect` values, and the generic `Runtime` holds the only mutable reference,
re-renders after each dispatch and hands the effects to the interpreter.
Handlers send messages, they never mutate. See PLAN.md, *State handling*.

**The outside world is injected.** `Shell.Dependencies` names every port (file
system, start path, quit callback, error reporter, root chooser, pattern
prompt, name prompt, confirmation) and `Shell.interpret` is the only place that
turns an `Effect` into a call on one of them.
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
`Runtime` involved. The side-to-view pairing lives in `FilePanel.Panels` and
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

**The background thread is a port, not a thread.** `Dependencies.RunBatch`
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
would quit the application.

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
`EditFile` carries the resolved `EditorPath option` beside the path, so
`Shell` and the adapters never look a setting up.

**Reading and writing properties happen inside a transition, like making a
directory.** `EditRequested` reads them (`EntryProperties.read`, mapped to
`ReportOperationError` when the entry is gone) and `PropertiesEntered` writes
them and re-reads the panel, so a directory that has just been marked hidden
disappears from a panel that conceals hidden entries without anyone asking for a
refresh. The popup itself is a port — `Dependencies.EditProperties` takes the
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

**The settings popup is a port too, and what it returns is state.**
`SettingsRequested` hands `state.Editors` to `Dependencies.EditEditors` through
the `ShowSettings` effect, along with the executable under the panel cursor if
there is one — the popup has no panel to look at, so what it can offer has to be
read before it opens; `EditorsEntered` puts the answer back into the state
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
`AppState.transferSource` answers a `TransferSource` — `FromDisk` of paths or
`FromArchive` of an archive and the `ArchivePath`s inside it — so `Update` maps
the two cases onto `CopyTargets` and `ExtractEntries` in two arms and never
asks a second time what kind of place the panel is standing in. The type is
what `F6` will match on when moving out of an archive joins it in P9.13. See
[ARCHIVES.md](ARCHIVES.md), *Copying out of an archive*.
