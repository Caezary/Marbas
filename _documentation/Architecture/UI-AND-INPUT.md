# UI and input

## Selection

**Selection is a set of names in the state; the ListView only displays it.**
`PanelState.Selected` holds entry names, not indices, so marks stay on the
right files when a refresh reorders the listing. Terminal.Gui's own marking is
used purely for rendering: the panel runs with `MarkMultiple = true` and
`ShowMarks = false`, which draws marked rows with `VisualRole.Highlight` and no
checkbox glyphs, and `FilePanel.render` re-applies `Source.SetMark` from the
state after every render. `Insert`/`Space` are handled by Marbas and marked
handled, so the built-in SPACE marking never runs and cannot disagree with the
state.

**One transition marks, however far it reaches.**
`AppState.toggleSelectionThrough side target` is the whole rule: take the run
from the cursor to `target`, mark or unmark all of it, put the cursor on
`target`. Everything else is a caller that works out `target` —
`toggleSelectionMoving` adds ±1 for `Insert`, `Space` and the shift arrows,
`toggleSelection` passes the cursor itself, and the shifted page keys pass what
`PageJump` measured. There is no second toggling code path to drift out of step
with the first.

**The run stops short of where the cursor lands.** Going down it is
`[cursor .. target-1]`, going up `[target+1 .. cursor]` — the entry started
from is always in, the entry landed on is always out. That is exactly what
`Shift+↓` does when repeated, which is what makes the page keys equal to
holding the arrow, and it is what makes a page key repeatable at all: an
inclusive run leaves the cursor on an entry it has just marked, so the next
press reads that mark, decides "unmark" and gives most of the page back. When
the cursor cannot move — the target is the cursor, at either end of the listing
— the run is the single entry it stands on, so nothing is unreachable and the
behaviour still matches `Shift+↓` pressed at the bottom.

**A run goes by the entry under the cursor, not entry by entry.** Toggling each
entry of a half-marked page separately would invert it, which is unpredictable
in exactly the case a user reaches for a page key. So `toggleSelectionThrough`
reads the mark under the cursor once and applies that one decision to the whole
run. Over a run of one entry it is plain toggling, which is why the same
function serves `Insert` as serves `Shift+PgDn`. And because there is one
`Selected` set per panel, all of it composes with the `+` / `-` patterns
without either side knowing about the other.

**Marking a page is a message with an index, because the page is the view's
business.** The state cannot know how tall a panel is, so `SelectionToggledTo`
carries a `PanelCursor` the way `CursorMoved` does: the wiring measures the page
with the same `PageJump` the `←`/`→` jumps use and says which entry to mark
through.

**The shift arrows are ordinary `KeyMap` bindings, which is what keeps them out
of the panel's other arrow rules.** `PanelWiring` asks `KeyMap.messageFor`
first, and `Key.CursorDown.WithShift` is a different key from `Key.CursorDown`,
so the page jumps on `←`/`→` and the edge-stopping on `↑`/`↓` never see them.
They are off the bar, like `Insert` and `Space`: holding `Shift` therefore
leaves the function-key row empty rather than showing a row of its own.

**The shifted page keys cannot be `KeyMap.bindings`, so `KeyMap` answers with a
direction instead.** `bindings` maps a key to a whole `Message`, and these four
keys only become a message once the view has measured the page. `KeyMap` keeps
what it can — `selectionPageDownKeys` and `selectionPageUpKeys`, two keys each,
and `selectionPageDirection` to look one up — so every key of the application
is still named in one file and `PanelWiring` decides nothing but the index.

## Navigation

**A page jump is a pure function of the page on screen.** `PageJump.forward`
and `PageJump.backward` take `{ FirstVisible; Height; Count }` and a cursor and
answer an index — the far edge of the visible page if the cursor is not on it
yet, otherwise a whole page further. The geometry comes from `ListView`'s own
`Viewport.Y` and `Viewport.Height`, which is the list's scroll offset and the
rows it shows, so a scrolled panel jumps from where it actually stands. The rule
lives in `Ui` because only `Ui` knows the geometry; what it produces is an
ordinary `CursorMoved`, so no new message carries a cursor.

**A key that must not leave the panel is one the panel marks handled.**
`ListView.MoveDown` answers `false` at the last entry, and an unhandled key
walks up to Terminal.Gui's focus navigation — which is why the cursor used to
land in the other panel. `PanelWiring` therefore ends in
`key.Handled <- handle …` and `stopsAtEdge` answers `true` for `↑`/`PgUp`/`Home`
at the top and `↓`/`PgDn`/`End` at the bottom: the panel swallows the key and
nothing happens, which is what the orthodox managers do. `←` and `→` were never
bound in `ListView` at all, so they escaped immediately; they are now handled
outright.

## Colour

**Colour is four schemes in one module; the views only ask for them.**
`ClassicScheme` holds `screen`, `panel`, `summary` and `keyBar` as
`Terminal.Gui` `Scheme` values, and four `SetScheme` calls put them on the
window, the two panel frames, the summary label and the status bar. Nothing
else in `Ui` mentions a colour. A scheme is inherited by every sub-view that
has none of its own, which is why the entry list and the status line need no
call: they take the scheme of the panel and of the window. Swapping the palette
is one file, and persisting a choice of scheme would not touch a view.

**Sixteen colours, on purpose.** Every attribute is built from `ColorName16`,
the DOS palette the classic managers themselves had, so nothing has to be
approximated on a terminal that offers no more than sixteen — which is what "respects
terminals with limited colours" comes down to. A test walks every colour the
schemes set and asserts it is exactly one of the sixteen, so a hand-picked RGB
value cannot slip in unnoticed.

**Rows are painted through `RowRender`, and only the ones nobody else owns.**
`ListViewRowEventArgs.RowAttribute` overrides whatever attribute `ListView`
chose for that row, marked and focused included, so the handler must hand the
row back when it is marked (`Source.IsMarked`) or under the cursor of a focused
list. What it needs is one attribute per row, which is why `FilePanel.PanelView`
is a `FrameView` carrying a `RowColors` array that `render` fills — the handler
is attached once and reads the array, rather than being re-subscribed on every
render. `OnRowRender` is public, so a test raises it and reads the attribute
back; nothing else about colour is observable without a driver.

**Which `VisualRole` a view draws with was measured, not guessed.** A view asks
its scheme for an attribute through the `GettingAttributeForRole` event, so
subscribing to it and drawing the view answers the question directly:
`ListView` uses `Normal` for ordinary rows, `Focus` for the row under the cursor
*only while it has focus*, and `Highlight` for marked ones; `FrameView` draws
its border and title with `Normal` whether focused or not; `StatusBar` uses
`Normal` and `HotNormal`. That settled the palette — the cursor bar disappearing
from the panel that lost focus is Terminal.Gui's own behaviour, and it is what
the managers Marbas follows do anyway.

**An entry says whether it is a program; `Ui` says what colour that is.**
`Executability.classify` mirrors `EntryVisibility.classify`: it takes the name
and the `UnixFileMode` and answers `Executable` for a Windows program extension
*or* for any execute bit, so both platforms' conventions are asked about on
both. `IFileSystemInfo.UnixFileMode` answers `None` on Windows rather than
throwing, and `MockFileSystem` lets a test set it, which is what makes the Unix
half testable from a Windows machine. Which colour an entry then gets is
`EntryColor.classify` — directory, archive (by extension), executable, plain —
and it is `Ui`'s alone, because an archive is a display grouping and not a fact
about the file.

## Sorting and hidden entries

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
The same bracket carries the hidden toggle when it is on
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

## Panel footer and search

**One rule for the size column, formatting per view.** `EntrySize.describe`
holds the shared decision — directories get `<DIR>`, files get their size, an
unknown size gets nothing — and takes the byte-to-text function as an argument.
The panel row passes `EntrySize.plain` (bare digits, to keep columns narrow),
the status line passes a grouped format with a `bytes` suffix. A new view, or a
future switch to `KB`/`MB`, only supplies another function.

**The bottom row of a panel has three states, and one module picks.**
`PanelFooter.describe` takes the whole `PanelState`: an open search wins, then
the marks, then what the directory holds. A blank row read as the end of the
listing, which is why it is never blank now and why a `Line` separates it from
the entries. `SelectionSummary` kept its job — it describes marked entries — and
`PanelFooter` is the choice above it.

## The key table and the bar

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

**A trap already hit.** An F# list literal that mixes plain elements with a
`for … -> …` comprehension keeps only what the comprehension yields; the plain
elements become ignored statements. `KeyMap.bindings` lost every function key
that way and only the two selection keys survived, which looked like a broken
key handler. The compiler does say so (`FS0020`) — the warning was there to be
read.
