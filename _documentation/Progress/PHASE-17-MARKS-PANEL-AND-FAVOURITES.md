# Phase 17 — The marks, the panel and the favourites

The tasks are in
[Phase 17 — The marks, the panel and the favourites](../Plan/PHASE-17-MARKS-PANEL-AND-FAVOURITES.md).

**P17.1 found the marks being erased by the list that drew them.** Of the two
suspects the plan named, it was the first: the panel's `ListView` ran with
`MarkMultiple = true` so that Terminal.Gui would paint marked rows in
`Highlight`, and in Terminal.Gui 2.4.17 every built-in move — `↑`, `↓`, `PgUp`,
`PgDn`, `Home`, `End` — ends in `ListView.SetSelection(item, false)`, which in
that mode clears every mark in the source before moving. `FilePanel.render`
was innocent in the sense that it did what it was built to do: a cursor move
leaves the rows `Kept`, so it had no reason to copy the marks in again, and the
footer, read from `PanelState.Selected`, went on counting what the screen no
longer showed. The side-arrow jumps were the exception that hid the pattern:
they go through the state and set `SelectedItem` directly, which marks nothing
and clears nothing.

The cure was to stop the list from marking rather than to keep repairing it
after every move. The panel's `ListView` now runs with `MarkMultiple` and
`ShowMarks` both off, so no move, click or `Ctrl+A` of its own ever writes a
mark, and the `RowRender` handler paints a marked row itself in the new
`ClassicScheme.markedEntry` — the same bright yellow on blue `Highlight` was.
The row under the cursor of the focused panel is still handed back to the
list, so it keeps the cursor bar; a marked row under the cursor of the panel
that lost focus is painted as marked. Re-applying `SetMark` on every render was
the other way, and was turned down: it would have been a pass over the whole
listing on every arrow key, undoing work Terminal.Gui should not have done in
the first place, and it would have left the mouse marking rows the state never
heard of.

Two theories in `PanelPaintingTests` press each of the eight movement keys over
a laid-out panel of forty files with two marks: one asserts the source still
holds both marks, the other that `RowRender` still paints both rows in the mark
colour. Before the fix the first failed for the six built-in moves and passed
for the two jumps; the second failed for all eight, the jumps included, because
a marked row used to be handed back to the list rather than painted. The look was then read off a running
`Application` with the `render-tui-screen` probe — three entries marked with
`Ins`, followed by `↓ ↑ ↓ PgDn PgUp → ← End Home ↓` — and compared with the
same run on the old `FilePanel.fs`:

```
              before                         after
after Ins     file01..03 BrightYellow,Blue   file01..03 BrightYellow,Blue
after Down    file01..03 BrightBlue,Blue     file01..03 BrightYellow,Blue
after PgDn    file01..03 BrightBlue,Blue     file01..03 BrightYellow,Blue
after End     file01..03 BrightBlue,Blue     file01..03 BrightYellow,Blue
after Home    file01..03 BrightBlue,Blue     file01..03 BrightYellow,Blue
```

Before the fix the marks went at the first `↓` and never came back; after it
they stand through every key, and give way to the cursor bar only on the row the
cursor stands on.

**P17.2 found that the grey keys cannot carry anything, and moved the phase onto
letters.** The key probe was rebuilt before it was run: a checklist that waits
for an exact `Key` can tick a key off but cannot say what came instead, and
instead was the whole question — a grey `+` that may arrive as a main one,
`Ctrl+M` as `Enter`, `Shift+Alt+1` as `Alt+!`. The new probe names one physical
key at a time and records whatever arrives at the application, at the view
Marbas would handle it in, and as a sequence the driver swallowed or a paste.
It ran by hand in cmder, Windows Terminal, the Windows console, WSL inside cmder
and gnome-terminal under WSLg.

The first round, 33 keys in the panel, three in the viewer and six in a dialog
holding a list, settled the plan's open question the hard way. No terminal tells
a grey key from its twin; `Ctrl` and `Shift` on either arrive as another key or
as nothing; `Alt` with punctuation — and so `Shift+Alt` with a digit — is sent
by every terminal as `ESC` and the character and dropped by Terminal.Gui's
`EscAsAltPattern`, which takes only letters, digits, control characters and `_`
after `ESC`. That last finding also explains why `Alt+\` arrived as nothing in
P13.1. Reading Windows Terminal's `defaults.json` and gnome-terminal's key table
named the rest: font size on `Ctrl` with `+` and `-`, a split pane on
`Alt+Shift+plus`, copy and paste on the `Ins` family and `Ctrl+Shift+C`/`V`,
tabs on `Ctrl+PgUp` and `Alt+1` … `Alt+0` once gnome-terminal has several, and
GNOME's own `<Alt>F8`.

The stand-ins were then chosen with the user — `Alt` to mark and `Alt+Shift` to
unmark, `Alt+I` and a bare `*` to invert, `Alt+M` for one step back, `Alt+K` and
a digit or `Ins` in the popup to keep a favourite, `Alt+B`, `Alt+U` and the
letters of phase 18 — and a second round pressed exactly those: all 19, the bare
`*`, and a digit and `Enter` in a dialog holding a list arrived as themselves in
all five terminals. The keys of phases 19 and 20 needed none. What each task now
binds is the table in
[the plan of phase 17](../Plan/PHASE-17-MARKS-PANEL-AND-FAVOURITES.md); the
measurement is in [Cross-platform](../Architecture/CROSS-PLATFORM.md).

**P17.3 marks by the entry under the cursor, on the letters P17.2 chose.**
`Alt+E` and `Alt+Shift+E` mark and unmark every entry sharing the extension of
the entry under the cursor, `Alt+N` and `Alt+Shift+N` every entry sharing its
name without the extension; the grey-key spellings are bound beside them. The
rule lives in `Domain`: a two-case `Likeness`, `SameExtension` or `SameStem`,
compares one part of two names without regard to case, and `EntryName` gained
`stem` beside the `extension` the sort by extension already had, so the two
commands and the sort agree on what an extension is — `gz` for
`archive.tar.gz`, nothing for `readme`, `.gitignore` or `odd.`.
`PanelState.applyLikeness` takes the cursor entry as the anchor and shares one
helper with the patterns, so a likeness and a pattern add to and take from the
selection in the same way. The tests run on a listing of files, directories,
double extensions and names without a dot, and one of them is what keeps `..`
out: by extension, `..` is alike to every name without one, and a mutation that
dropped the `selectable` filter was caught by it. Two key tests drive `Alt+E`
and `Alt+Shift+N` through a real panel.

**P17.4 marks all files and inverts.** `Alt+A` marks every file and
`Alt+Shift+A` unmarks every file, both leaving the marks of directories as they
were; `Alt+I` and a bare `*` invert the marks of every entry, directories
included, with `Shift+grey+`, `Shift+grey-` and `Ctrl+grey*` bound beside them.
Both transitions sit beside the patterns and the likeness in `PanelState` and
take no anchor from the cursor, so `..` under it makes no difference and `..`
itself is never in the set. The bare `*` is the one change a user will notice
outside marking: it no longer opens the quick search, though it still types into
a search already open, as `+` and `-` always have. The tests were run red first
against transitions that changed nothing — nine failed, the tenth, inverting
twice, holds for any transition that changes nothing and passed by nature —
then green against the real ones. They cover a listing with marked and unmarked
files and directories, and each of the three after a pattern applied before it.

**P17.5 gives the marks one step back.** `Alt+M` swaps a panel's marks with the
set they held before its last marking command, so a second press goes forward
again; `Ctrl+M` is bound beside it although every terminal measured turns it
into `Enter`. The memory is a field of `PanelState`, recorded by one helper every
marking transition goes through, and forgotten in one post-step of
`Update.update` for every message that is not a marking command or one of the
exceptions. Two cases the plan left open were settled on the way and written
back into its decision: a marking command that changes no mark records nothing,
and the refresh the directory watch raises keeps the memory — the plan's clause
about names that have vanished by the time the marks come back only has
something to do if a listing can change while the memory lives, and the watch is
the one thing that changes it without being asked. The tests send messages
through `Update.step`, so the forgetting is tested where it happens: every kind
of marking command undone and redone, a file operation and a change of
directory forgetting, five kinds of message keeping, vanished names left out,
and each panel keeping its own memory. Run first against a transition that did
nothing, nineteen failed; four passed by nature, and three mutations afterwards
— no forgetting, no carrying across the watch's refresh, recording without a
change — were each caught. The fourth, the change of directory, holds even
without the post-step, because a newly loaded listing starts without a memory.

**P17.6 goes up one level.** `Ctrl+PgUp`, with `Alt+U` beside it for
gnome-terminal and its tabs, does what `Enter` on `..` does without needing the
cursor on `..`: `AppState.upOneLevel` picks the way out by where the panel
stands and calls the very functions `..` reaches — the parent directory with the
cursor on the directory left, the level above inside an archive, out of an
archive at its top level, and back from search results to where the search was
started, on the entry the panel stood on then. The last of those was written
twice already, so it became one `backToSearchStart`. On a root the parent is
missing and nothing changes. The landing reaches the folder history through the
post-step every landing goes through. The tests, red first against a function
that went nowhere, cover the four places and the root on `MockFileSystem`, the
history, both bindings, and both keys pressed in a real panel; the root case
passed against the empty function, as a case that expects nothing to happen
does.

**P17.7 spells sizes compactly in the listing.** `EntryFormat` now hands
`CompactSize` to `EntrySize.describe`, so a file, a measured directory — with
its `≥` when measured in part — and a hit in a panel of search results read
`512 B`, `1.5 KB` or `2.0 TB`; `EntrySize.plain` lost its last caller and went.
The footer and the status line keep the exact bytes. The tests turned up a
defect the change also mends: bare digits needed eleven columns from ten
gigabytes on, and a row holding such a file was wider than its panel; the test
that every row is as wide as asked failed on it before the change and passes
after. The column did not get narrower, as the plan hoped: `≥1023.0 KB` is ten
characters, exactly the old width. The results window of `Alt+F7` formats its
hits through the same function, so it changed with the panel, and its one test
that expected `1024` now expects `1.0 KB`.

The screen was read off a running `Application` with the `render-tui-screen`
probe — both panels drawn by the real `FilePanel.render` from a listing of
synthetic entries, since a mock file cannot hold a terabyte:

```
║photos                           59.6 GB 2026-10-03 12:30║
║backups                       ≥1023.0 KB 2026-10-03 12:30║
║empty.txt                            0 B 2026-10-03 12:30║
║note.txt                           512 B 2026-10-03 12:30║
║kilo.bin                          1.5 KB 2026-10-03 12:30║
║film.mkv                          9.3 GB 2026-10-03 12:30║
║disk.img                          2.0 TB 2026-10-03 12:30║
║─────────────────────────────────────────────────────────║
║7 files, 3 folders   2,209,029,025,791 bytes             ║
```

The footer's figure is the exact sum of the seven files, and the other panel,
with two of them marked, showed `2 selected   2,199,023,257,088 bytes`, the
exact sum of those two.

**P17.8 opens the drive popup beside its panel.** The plan's wording allowed two
readings — the popup centred over its panel and pushed back onto the screen when
wider than it, or the popup standing at the panel's outer edge — and the user
chose the second: two columns in from the outer edge, reaching inwards, over the
other panel when it is wider than its own. `ChooseDrive` now takes a
`DrivePrompt` carrying the rows and the `PanelSide`, which `Shell` already had
from `PromptForRoot`, and `DriveDialog.column` turns the side, the screen width
and the popup's width into the column it starts at; it moves only when the popup
is wider than the screen less the margin, the margin giving way first. A probe
showed that Terminal.Gui pulls no window back onto the screen — a dialog at
column 90 of a 120-column screen is simply cut off — and `Pos.GetAnchor` is
internal, so the column is a `Pos.Func` over a screen width the adapter reads
from `IApplication.Screen`, worked out again on every layout.

The tests were run red first against a `column` that answered 0 and a dialog
that ignored the side: ten failed, the cases where the right answer is 0 passing
by nature. They cover the column for both sides on wide, eighty-column and too
narrow screens, the frame of a laid-out dialog on 80 and 200 columns, and the
side reaching the chooser from `Shell` and from `Alt+F1` / `Alt+F2` in a real
panel. The screen was then read off a running `Application` with the
`render-tui-screen` probe, the popup opened over the real main window and the
screen resized under it:

```
80 columns, Alt+F1
║ ╔╡Drives╞═════════════════════════════════════╗                              │
║ ║ C:\  System  fixed  931.5 GB  120.0 GB free ║                              │
║ ║ D:\  Data    fixed    1.8 TB    1.2 TB free ║                              │
80 columns, Alt+F2
║                              ╔╡Drives╞═════════════════════════════════════╗ │
║                              ║ C:\  System  fixed  931.5 GB  120.0 GB free ║ │
║                              ║ D:\  Data    fixed    1.8 TB    1.2 TB free ║ │
```

On 200 columns the left popup stood at column 2 and the right one ended at
column 198, each well inside its own hundred-column panel. The popup was
created on a 120-column screen and resized to 80 and 200 before the shot, so the
same pictures prove that it follows the edge when the terminal changes size.

**P17.9 keeps ten directories under the digits.** `Alt+K` keeps the place the
active panel stands in under a digit asked for next, and `Alt+0` … `Alt+9` send
the active panel there. The plan's "asks for the digit on one line" left the
shape open, and the user chose a popup of one line over a question in the status
line: `KeepFavoriteDialog` has no buttons, and its first key decides — a digit is
the slot, a digit with `Alt` still held from `Alt+K` too, and anything else,
`Esc` included, keeps nothing. The status line would have needed a waiting mode
in `AppState` and a rule for every other message arriving while it waited. A
first version asked *Press a digit to keep this place under it*; at the user's
word the popup is titled *Add to favourites* and names what it will keep —
*Press a digit to add* the path *to favourites* — so the port is handed the
place, and a path too long for the screen is shortened in the middle, the way
the panel titles shorten theirs. The user offered two lines, a sentence and the
full path under it, in case one line proved hard; it did not, since the popup
is as wide as its line and only the screen bounds it.

The pieces follow the history's. `FavoriteSlot` in `Domain` is a digit from 0 to
9 with a private constructor. `Favorites` in `FileSystem` is a map from slot to
`Place`, with `apply`, `format`, `parse`, `load` and `change` on the pattern of
`History`, its own `FavoritesError`, and `favorites.conf` beside the settings.
The state raises `PromptForFavoriteSlot place` for `Alt+K` — nothing for a panel
of search results — and `LookUpFavorite slot` for `Alt+digit`; `Shell` writes
the slot answered, or reads the file afresh and answers `FavoriteFound place`,
which stands the panel in it through `standInside`, so a place that has gone is
the listing error every landing reports and the folder history remembers the
landing through the usual post-step. Nothing reads the file at startup.

The file module, the transitions, `Shell`, the key table and the popup were each
run red first against a stub that did nothing: five, seven, four, seven and four
tests failed, and the rest — the cases that expect nothing to happen, and on the
file side the cases a stub that formats nothing satisfies, among them the change
on top of another copy's file — passed by nature. The seven tests that press
`Alt+K` and `Alt+3` in a real panel were written after the pieces and passed at
once; they prove the path from key to file and back, not a piece of it. A
running `Application` with the real adapters, over directories in the
scratchpad, then showed the popup and what the keys did:

```
80 columns
║           ╔╡Add to favourites╞══════════════════════════════════╗            │
║           ║ Press a digit to add C:\...\fav\start to favourites ║            │
║           ╚═════════════════════════════════════════════════════╝            │
```

On 200 columns the same popup named the whole path, a line of more than a
hundred and fifty characters, and stood inside the screen.

`3` closed it and `favorites.conf` held `favorite.3` beside the `favorite.5` the
file was started with; `Alt+5` then put the left panel in that slot's directory
and `history.conf` remembered it. A second run pressed `Esc` instead of `3`: the
popup closed, the file kept only `favorite.5`, and the application went on to
answer `Alt+5`, so `Esc` closes the popup and not Marbas.
