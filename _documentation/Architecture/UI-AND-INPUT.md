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

**Colour is five schemes in one module; the views only ask for them.**
`ClassicScheme` holds `screen`, `panel`, `summary`, `keyBar` and `keyName` as
`Terminal.Gui` `Scheme` values, and a handful of `SetScheme` calls put them on
the window, the two panel frames, the summary label, the bottom bar and the key
name inside each of its slots. Nothing
else in `Ui` mentions a colour. A scheme is inherited by every sub-view that
has none of its own, which is why the entry list, the status line and the bar's
command labels need no call: they take the scheme of the panel, of the window
and of the bar. Swapping the palette
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

**Names collate, they do not compare byte by byte.** Names and extensions go
through `StringComparison.InvariantCultureIgnoreCase`, which puts punctuation
before digits and digits before letters — so `_notes.txt` heads the listing,
where an ordinal comparison put it after `zebra` because `_` is `0x5F` and the
letters it was being weighed against uppercase to `0x41..0x5A`. Invariant rather
than current culture, so the same directory lists in the same order on every
machine; `InvariantGlobalization` therefore has to stay off, which
[PACKAGING.md](PACKAGING.md) records.

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
survive. That is deliberate: `F5`, `F6`, `F8`, `Shift+F1` and `Shift+F2` work on
the marks,
and a mark nobody can see is a file operated on by accident.

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

**The directory line counts both kinds, the total counts one.** `3 files, 2
folders   1,234 bytes` answers "what is in here" for a panel that is mostly
subdirectories, which a file count alone did not. `..` is a way out of the
directory rather than something in it, so it is left out of both counts, and
folders carry no size, so only the files add to the total. The nouns are
pluralised, because `1 files` is the kind of detail a file manager is read on
all day.

**A file name is content, not markup.** Terminal.Gui reads `_` in a `Label`'s
`Text` and in a `FrameView`'s `Title` as the hot key specifier: it takes the
underscore out of what it draws and binds the letter after it as a shortcut, so
`_notes.txt` reached the status line as `notes.txt` and a panel sitting in
`C:\start\_sub` claimed `Alt+S` for itself. `LiteralText.showing` sets
`HotKeySpecifier` to `0xffff`, which is Terminal.Gui's "this view has no hot
key", and every view that displays something the file system named goes through
it — the two panel frames, the panel footers, the status line and the progress
dialog's line. The views whose text Marbas writes itself keep the default, which
is what makes `_Cancel` and `_Ok` work on the dialog buttons.

## Popups

**Every popup is dressed once, in one place.** `DialogStyle.apply` runs from
`Program.fs` right after `Application.Init` and sets what Terminal.Gui keeps as
process-wide defaults: a double frame on `Dialog` and on `MessageBox` — the
frame a focused panel wears — buttons centred under the question, no shadow on
either, and `[` `]` for the brackets drawn around a button. It also registers
`ClassicScheme.dialog` and `ClassicScheme.errorDialog` under the `Dialog` and
`Error` scheme names, which is the only way to reach a `MessageBox`: it builds
and runs a dialog of its own, so nothing outside it can call `SetScheme`. After
`Init` rather than before, because the configuration manager fills those
statics in as the application starts and would overwrite an earlier call.

**What it replaces was not a matter of taste.** 2.4.17 ships a `Heavy` frame,
an opaque shadow under every button and `⟦ ⟧` (U+27E6/7) for the brackets. Those
brackets and the heavy box glyphs are missing from the usual console fonts, so
they came out as gaps and boxes, and a button's shadow is drawn *outside* the
button — which pushed the row past the frame and squeezed the prompt's label and
input field off the dialog altogether. Rendering a popup into
`IDriver.Contents` and reading the screen back is what settled each of them;
what the suite can assert without a driver is the frame, the shadow, the
alignment, the brackets and the scheme.

**A shadow can only be taken off before the popup exists.** `DialogStyle.dress`
puts the frame, the button alignment and the scheme on a dialog of ours one by
one, so those hold whatever the process-wide defaults happen to be — but a
shadow is a `Margin` thickness fixed while the view is constructed, and setting
`ShadowStyle`, `Margin.ShadowStyle` or the thickness itself afterwards changes
nothing. `Dialog.DefaultShadow` and `Button.DefaultShadow` are therefore the
only levers, which is why `apply` has to run before the first popup is built —
`Program.fs` calls it two lines after `Init`, and every test that lays a dialog
out calls it first. That is also how the suite caught it: with the shadows
back, the content area of a six-row dialog measures zero.

**`Esc` closes a popup because the quit command answers to it.** Terminal.Gui
ends a running `Dialog` on `Application.GetDefaultKey Command.Quit`, `Esc` by
default; binding that command to `Ctrl+Q` and `F10` had taken `Esc` from every
popup, the progress window included. The binding now carries all three, and
`PanelWiring.swallowed` consumes `Esc` in the panel — where it cancels a search
or does nothing — so it never reaches the application binding and quits.
Application-level bindings are consulted last, after the focused view has had
the key, which is what makes swallowing it enough.

**A prompt suggests, it does not decide.** `PromptDialog.create` takes the text
to open with and puts the caret behind it, so `F6` opens on the other panel's
path with the end of it ready to edit. `F7` adds `PromptDialog.selecting`, which
selects the whole suggestion: the name of the last directory made — see
[SETTINGS-PERSISTENCE.md](SETTINGS-PERSISTENCE.md) — is one keystroke from being
replaced and `Enter` alone accepts it. Selected text is drawn with
`VisualRole.Active`, which is why the dialog scheme gives that role a colour of
its own instead of the one the input field wears; with both black on cyan the
selection was invisible.

**A field that is typed into stands out from the popup it sits in.**
`DialogStyle.typedInto` puts `ClassicScheme.input` on a `TextField` — black on
cyan, black on white where the caret is — because a popup with more than one
field cannot rely on focus to show where the fields are. `PromptDialog` has one
field and it is always focused, so it keeps the dialog scheme; the properties
and settings popups have three each and would otherwise draw them white on blue,
indistinguishable from the label beside them. It was the rendered screen that
said so, not a property: an empty unfocused field is invisible to every test
that reads `Frame` and `Text`.

**The properties popup is a value in and a value out.** `PropertiesDialog.create`
takes an `EntryProperties`, ticks a `CheckBox` per `EntryFlag` and writes each
timestamp through `TimestampText` into a field; `PropertiesDialog.properties`
takes the original back and folds the boxes and the fields onto it. Reading the
original in is what makes a timestamp the user mistyped harmless — it is left as
it was rather than zeroed — and it is what keeps the path untouched, since
nothing on screen can change it. `TimestampText` fixes one pattern,
`yyyy-MM-dd HH:mm:ss`, on the invariant culture, so what a machine in another
locale shows is what it reads back. The pattern carries no offset on purpose —
a file's stamps are local and `+02:00` on every row would be noise — so a
`DateTimeOffset` is written in the terms of its own offset and text typed back
is read with `AssumeLocal`. For a stamp that came off the file system, which is
the only kind the popup ever shows, those are the same offset.

**A tick box is marked with signs a console font carries.** Terminal.Gui draws
`CheckState` with U+2610 and U+2611, which are in the same class of glyph as the
brackets `DialogStyle` already replaced. `apply` sets them to a filled square
and a middle dot — both CP437 — so a checked flag reads at a glance and an
unchecked one still shows where the box is.

**The settings popup keeps its own working copy, so it needs no nested modal.**
`SettingsDialog.SettingsView` holds a `ProgramSettings` in a field and edits it
in place: the extension and program fields plus `Add` make one association
(`ProgramSettings.associate`, which replaces an extension already known in that
role), `Remove` takes the extension in the field back out, and moving the cursor
in either list fills both fields from the row under it. A second dialog for adding a row
would have needed `app.Run` from inside a dialog, which only `Prompts` may do;
this way the whole popup is one view and every one of those steps is a test
that never runs an application loop. `Enter` in either of the two fields does
what `Add` does and is marked handled there, because the alternative is what
Terminal.Gui does by default — bubble to the dialog's last button, which is
`Ok`, closing the popup and throwing the association away.

**The role is a choice on the popup, and each role has a list of its own.**
`Used for` is an `OptionSelector` of `Edit` and `Open`, and `Add` and `Remove`
both act in the role it names, so one pair of fields serves both halves of the
table. Below them the popup shows `Edited with:` and `Opened with:` as two
lists, because a row belongs to exactly one role and a single list would have
had to spell that out on every line. Picking a row out of either list fills the
fields *and* moves the role selector to that list's role, so the row a cursor
stands on is the row `Add` and `Remove` would change. Re-reading the lists after
a change raises `ValueChanged` on both, which would otherwise scribble over the
fields, so the view sets a `refreshing` flag over the reload and the handlers
stand down while it is up.

**`Under cursor` saves retyping the path of a program the panel is already
standing on.** `SettingsRequested` reads the entry under the cursor and, when it
is an executable (`Executability.Executable`, which is the extension on Windows
and the exec bit on Unix), hands its path to the popup as a candidate; `Alt+U`
puts it into whichever program field was last focused, the default editor until
another has been typed in. With no executable under the cursor there is no
candidate and the button is disabled, which says so on the screen rather than
answering a keystroke with nothing. Making it a button rather than a bare
binding is what makes it discoverable: a hotkey nobody can see is a hotkey
nobody presses.

## The key table and the bar

**One table holds every key, and the bar is a view of it.** `KeyMap.bindings`
is the whole keyboard: key, an optional bar label, and the `Message` the key
stands for. `KeyMap.messageFor` is what the panel's single `KeyDown` handler
asks, and `KeyMap.bar modifiers` is what the bottom bar draws. Adding a
command is one line, and a command cannot be on the bar under one name and do
something else when its key is pressed, because both come from the same row.
There was a `PanelInput` module holding the panel half of this; P7.5 merged it
into `KeyMap` and deleted it.

**The bar is twelve slots, not a list of what happens to be bound.**
`KeyMap.bar` walks `KeyMap.functionKeys` — `F1` … `F12`, in order — applies the
held modifiers to each with `Modifiers.apply` and looks the result up in
`bindings`. It always answers twelve `BarSlot`s: a name (`F1` … `F12`), the
key the modifiers make of it, the command's label or `""`, and the `Message` or
`None`. A slot's label and its message come out of the same lookup, so a slot
cannot show a command it would not run. `Modifiers.apply` is the inverse of
`Modifiers.ofKey`, which is what makes "the `Ctrl` row" mean `Ctrl` laid on
every function key rather than a filter over the table.

**Only a function key carries a label, because only a function key has a
place.** `Alt+H` used to be labelled `Hidden` and rode the `Alt` row; the bar
now has no slot to put it in, so it is `offBar` like `Ctrl+H` and `Ctrl+R`. A
test asserts that every labelled binding is some function key with modifiers on
it, so a label on a key with nowhere to go fails the suite rather than
disappearing quietly.

**Where the slots sit is arithmetic, and the arithmetic is its own module.**
`KeyBarLayout.cell count index width` answers `{ X; Width }` from
`index * width / count` — proportional division, so the remainder is spread
along the row instead of heaped on one end and no two slots differ by more than
a column. `FunctionKeyBar` hands it to `Pos.Func`/`Dim.Func` against the bar's
own `Viewport.Width`, which is re-evaluated on every layout pass, so a resized
terminal re-tiles without a handler of its own.

**Two backgrounds means two views, which is also what makes it testable.** A
slot is a `SlotView` holding a `Label` for the key name — wearing
`ClassicScheme.keyName`, white on black — and a `Label` for the command, which
inherits the bar's `keyBar`, black on cyan. Painting one line in two attributes
by hand would have been fewer views and unassertable: there is no driver in a
test, so what a test can read back is a view's `Scheme`, not a cell. Because the
names never change, `FunctionKeyBar.show` only writes the command labels and the
messages; the slots themselves are built once.

**The bar shows keys, it does not take them.** It is a plain `View` with no key
bindings at all, so `F5` is handled where every other key is — the `KeyDown`
handler on the focused panel's `ListView`. That kills the trap that made `F7`
unreachable from the panel, and it means a function key does nothing while a
dialog holds focus, which is what it should do anyway. The price is that tests
can no longer send `F5` to the window and have the bar catch it, so
`Gui.pressKey` sends it to the panel that has focus, the way a user does. The
mouse is the one input the bar does own: a left click on a slot, or on either of
its labels, dispatches whatever `Message` the slot is currently carrying.

**Held modifiers are polled, because the driver will not report them.**
Terminal.Gui 2.4.17 has the vocabulary for it — `Key.IsModifierOnly`,
`Key.ModifierKey` — but only the kitty keyboard protocol fills it in. The
Windows input processor returns from every key-up before anything is raised
(`if (!inputEvent.KeyEvent.bKeyDown) { return; }`) and never surfaces a
standalone `Ctrl`, so there is no event to listen to on the platform Marbas is
developed on. `Dependencies.WatchModifiers` is therefore a port that hands over
a callback; `ModifierWatch` is the only place that knows about
`GetAsyncKeyState` and `app.AddTimeout`, polls ten times a second and reports
only when the set changes. On anything but Windows it registers nothing and
the bar keeps the classic row. Everything above the port — which row a set of
modifiers means, what the bar then shows — is ordinary tested code, because
tests call the captured callback themselves.

**A row is what the table says, empty included.** Holding `Shift` leaves every
slot without a command, because no command is bound to a shifted function key.
Falling back to the classic row would say that `Shift+F5` copies, which it does
not. The slots stay where they are while it happens, so the bar reads as twelve
keys with nothing on them rather than as a bar that vanished.

**Twelve slots want a wide terminal, and clipping is the honest answer.**
`F10` plus a command in one twelfth of eighty columns leaves three characters,
so `Quit` is drawn as `Qui` there; from about ninety columns every label fits.
Shortening the labels would buy the narrow case at the cost of the wide one,
and the bar is meant to be read at the width a file manager is actually run at.

**A trap already hit.** An F# list literal that mixes plain elements with a
`for … -> …` comprehension keeps only what the comprehension yields; the plain
elements become ignored statements. `KeyMap.bindings` lost every function key
that way and only the two selection keys survived, which looked like a broken
key handler. The compiler does say so (`FS0020`) — the warning was there to be
read.

## The viewer's own screen

**The viewer is a screen, not a dialog.** `F3` runs a second `Window` with
`BorderStyle = LineStyle.None`, exactly the way the main screen is built, so it
covers the terminal edge to edge with no frame to eat four columns and no title
bar to repeat what the header already says. Row 0 is the header, the last row is
a key bar, and everything between is the content.

**Its key table is its own.** `ViewerKeys` is a second, much smaller `KeyMap`:
the arrows move by a line, `PgUp`/`PgDn` by a page, `Home`/`End` reach the ends
of the file, the side arrows move across a long line — a column at a time, a
screen at a time with `Ctrl` held — `F2` wraps, `F4` switches to the hex dump
and back, and `F3`, `F10` and `Esc` leave. Nothing is bound to the panel's
table while the viewer is up, because the viewer's content view is what holds
focus and keys do not bubble.

**Both bars are the same bar.** `FunctionKeyBar.build` makes the twelve-slot
row — the proportional layout, the two schemes, the `SlotView`s — and both the
panel bar and the viewer bar are built from it; `show` fills the panel's slots
from `KeyMap`, `label` fills the viewer's from `ViewerKeys.bar`, which asks for
the mode and the wrap so `F4` reads `Hex` in text and `Text` in hex.

**The content view draws itself, because nothing in the toolkit fits.**
`TextView` was deprecated in 2.4.17, and it was the wrong shape anyway: it owns
a document, and the point of the viewer is to own nothing and read the file
through a window. `ContentView` overrides `OnDrawingContent` and paints the
rows of the current viewport, padded to the width so the row beneath is erased
without a clear. Because the rows come from `Rows()`, which a test can call,
scrolling, wrapping and the hex switch are all assertable without a driver.

**Wrapping moves by whole lines.** With `F2` on, one line can take several
rows, but the top of the screen is always the start of a line — a page down
advances by however many lines the screen held (`ViewerScroll.filling`), not by
a row count. That keeps the line index the one measure of position, and it is
what a wrapped page up and a percentage in the header are both counted from.
