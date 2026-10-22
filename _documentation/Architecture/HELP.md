# Help

Phase 24. The user guide, read on GitHub and shown full screen under `F1`.

## The keys of the help

| Key | Alternative | Action |
|-----|-------------|--------|
| `F1` | | Open the help: in a panel, the viewer or a popup |
| `Tab` | | Next link on screen |
| `Shift+Tab` | | Previous link on screen |
| `Enter` | | Follow the link under the cursor |
| `Backspace` | `Alt+←` | Back to where the link was followed |
| `F7` | | Search the chapter on screen |
| `Shift+F7` | | Repeat the last search without the popup |
| `↑` `↓` `PgUp` `PgDn` `Home` `End` | | Scroll, as in the viewer |
| `Esc` | `F10` | Close the help |

`F1` has no spare. P24.1 measured it, `Tab`, `Shift+Tab`, `Backspace` and
`Alt+←` in the five terminals of the checklist, and every one arrived in every
place it is meant for; see
[Cross-platform](CROSS-PLATFORM.md#keyboard-and-terminal). `Ctrl+F1`, `Alt+S`
and `Alt+Y` were measured beside `F1` as spares and arrived as well; none is
bound. `F7`, `Shift+F7`, `Esc` and `F10` are the viewer's keys and were
measured for a full-screen window before.

`Backspace` and `Alt+←` mean other things in a panel, where they edit the quick
search and walk to the previous tab. The help is a full-screen window of its
own, so a key there never reaches a panel.

## The model of a chapter

`ChapterMarkdown.parse` answers a chapter as a list of `Block`s, or the first
`Refusal` in it: the construct outside the subset and its line, counted from
one. `Guide/Block.fs` names every level rather than nesting lists:

| Type | Is |
|------|----|
| `Span` | `Text`, `Code`, `Strong` or `Emphasis` of a phrase, or `Link` of a phrase and a `Target` |
| `Phrase` | a run of spans: what a paragraph, a heading, a cell and an item read |
| `Item` | a phrase and the items nested under it |
| `Prose` | a `Paragraph` or `Bullets`, a list of items |
| `Table` | a header `Row` and body rows of `Cell`s |
| `Heading` | its level, its `Anchor` and its title |
| `Block` | a heading, prose, a table, or an `Alert` of `Note` or `Warning` and its prose |

- `Text` and `Code` carry a `string`: the text has been parsed, and nothing
  after the parser reads anything into it. Adjacent text is joined, so an
  escape, an entity or a line break inside a paragraph leaves one `Text`.
- A `Target` is `SameChapter` of an anchor, or `OtherChapter` of a
  `ChapterName` — a `.md` file beside the chapter, no directory — and perhaps
  an anchor. Anything else is `LinkOutsideTheGuide`; whether the chapter and
  the heading exist is checked against the whole guide, not here.
- An item holds one paragraph and at most one nested list; an alert holds
  paragraphs and lists. A second paragraph in an item, or a table in an
  alert, is refused, so the layout never has to place one.
- `Table.create` is the only way to a `Table`, and it pads or cuts each body
  row to the header's width, as GFM does. Markdig widens the header by an empty
  cell when a body row is longer; the parser cuts the header back to the
  columns of the delimiter row.
- The anchor is Markdig's `AutoIdentifierOptions.GitHub`: lower case, spaces to
  `-`, punctuation dropped, `-1`, `-2` … for a repeated heading.
- Refused: a heading below `###`, an ordered list, a code block, a plain quote,
  an alert other than `NOTE` and `WARNING`, a thematic break, HTML, an image, a
  hard line break, an aligned column, a link reference definition, and any
  node Markdig makes that the parser does not name (`Unrecognised`).

## The chapters

`docs/user-guide/` holds `README.md`, the contents, and sixteen chapters
numbered in reading order, `01-getting-started.md` to `16-platforms.md`. They
replaced `_documentation/USER-GUIDE.md` in P24.3.

- A chapter opens with one or two sentences, then a key table per group of
  commands, then bullets with the rules. A popup's own keys are a second
  table under a sentence naming the popup.
- A key table has the header `Key`, `Alternative`, `Action`. A table about
  anything else has headers of its own (`Kind`, `Write`, `Line`, `Variable`,
  `File`, `Platform`), so a test can pick the key tables by their header.
- A `Key` or `Alternative` cell holds one inline code span per key:
  `` `↑` `↓` ``. A run of keys is its first and its last with `…` between,
  `Alt+0` … `Alt+9`. Modifiers are written `Ctrl`, `Alt`, `Shift`, in that
  order.
- A platform difference is a `[!NOTE]` or `[!WARNING]` under the table of its
  key, and a row or a bullet of `16-platforms.md`.
- Left out because no terminal measured delivers them: the main `+` and `-`
  with `Ctrl`, `Alt` or `Shift`, `Ctrl+*`, `Ctrl+M`, `Shift+Enter`,
  `Ctrl+Alt+Enter`, `Ctrl+Shift+C`, `Ctrl+Shift+X` and `Ctrl+Shift+V`. `Ctrl+\`
  arrives as `Ctrl+4` and is named in a rule only. All of them are rows of
  `KeyMap.bindings`, listed by name in
  [the test of the keys](#the-guide-and-the-key-maps). `F1` is a row of
  *Keys everywhere* and of the viewer's table.
- A link leads only to a chapter or a heading of the guide, since the parser
  refuses anything else; the releases page is named in words. The format of
  `user-menu.conf` is a table of lines, since a code block is refused.

`Marbas.fsproj` embeds the chapters as `user-guide/<file>`, shown in the
project under `UserGuide`. `EmbeddedGuide.names` answers them, and
`EmbeddedGuide.read` reads one by its `ChapterName` and parses it, answering
a `Chapter` or a `GuideFault`: `Absent` for a name no resource has, `Refused`
with the parser's `Refusal`. The resources are the binary's own, so the read
is no port. `ChapterName.contents` is `README.md`, the first page.
`EmbeddedGuideTests` compares the names with the `.md` files of
`docs/user-guide/`, found from `__SOURCE_DIRECTORY__`, the one test that reads
the repository. `UserGuideTests` holds the guide through `EmbeddedGuide`: it
fails on a file missing from or added to the seventeen it names, on a chapter
the parser refuses, and on any link `ChapterLinks.broken` reports. That function takes every `Chapter`, a
`ChapterName` and its blocks, gathers each link from headings, paragraphs,
items at any depth, table cells and alerts, and answers the links whose
chapter is not among them or whose anchor is no heading of that chapter.

## The guide and the key maps

`KeySpelling.nameOf` spells a Terminal.Gui `Key` as the `KeyName` a key table
writes: the modifiers held, in the order `Ctrl`, `Alt`, `Shift`, each followed
by `+`, then the key.

- A key without a character has a name: `↑` `↓` `←` `→`, `PgUp`, `PgDn`,
  `Home`, `End`, `Ins`, `Del`, `Enter`, `Esc`, `Backspace`, `Tab`, `Space`,
  `F1` to `F12`.
- A letter is its capital, so `Key.E.WithAlt` is `Alt+E`. `Key 'A'` is a
  capital typed with `Shift` and is `Shift+A`.
- Any other character is itself: `Key.D4.WithCtrl` is `Ctrl+4`, `+` with
  `Shift` is `Shift++`.
- A key with neither keeps the name Terminal.Gui gives it, `PrintScreen`.

`ChapterKeys.named` answers the `KeyName`s of a set of chapters: every inline
code in the first two cells of a body row of a table headed `Key`,
`Alternative`, `Action`. Two codes with `…` between them that differ only in
the last character are a run and name every character from the first to the
last, so `Alt+0` … `Alt+9` names ten keys. The `Action` cell and tables under
other headers, the one of `16-platforms.md` among them, name none.

`GuideKeysTests` spells the keys the code binds: `KeyMap.bindings`;
`KeyMap.selectionPageDownKeys` and `selectionPageUpKeys`, which the panel asks
outside the bindings; `KeyMap.historyKeys`, `userMenuKeys` and
`favoritesKeys`, the keys of those popups with the letters `a` to `z` and the
digits of the menu's hotkeys and the digits of the favourites; and
`ViewerKeys.keys`, `FindKeys.keys` and `HelpKeys.keys`. It compares them with the whole guide,
not with the chapter of their context, and fails on:

- a key bound that no key table names, except the keys no measured terminal
  delivers, listed by their `KeyMap` names;
- a key a table names that nothing binds, except those Terminal.Gui's views
  handle: `↑`, `↓`, `PgUp`, `PgDn`, `Home`, `End`, `Tab`, `Enter`, `Esc` and
  `Alt+↓`, which opens the list of a field that remembers;
- a key no measured terminal delivers that a table names.

The popups test a key against each named key in turn, so their lists are read
by the test alone, and a key a popup starts to test without joining its list
escapes it. `Backspace`, a literal of the quick search and of the history's
filter before, is `KeyMap.takeBackKey`, so its rows have a binding. Setup's
`Alt+U`, the hotkey of *Under cursor*, and the drive popup's `Alt+R` pass
because the panel binds the same keys. Replacing the `Ctrl+R` row of
`02-panels.md` by an `Alt+Q` row failed the first check on `Ctrl+R` and the
second on `Alt+Q`.

## The layout

`ChapterLayout.lay width blocks` answers the lines of a chapter for a screen
`width` columns wide. A `LaidLine` is a list of `Piece`s, the `Anchor` of
the heading it starts, if it starts one, and the index of the `Block` it lays
out, the blank line before a block counted with it. A `Piece` is text with a `Look` —
`Plain`, `Literal` for inline code, `Title` of a heading level, `Ruling` for
the rules of a table, `Framing` of an alert kind — a set of `Stress`, `Bold`
and `Italic`, and the `LinkStop` it belongs to. Adjacent pieces of one style
are merged. A column is one character: the guide uses no wide characters.

- Blocks are parted by one blank line. A paragraph is reflowed: words, split
  at spaces, fill a line greedily. Inline code is never split at its own
  spaces. A word wider than the line is cut at the width. The space between
  two words takes their look and stress when they share them, and their link
  when they share one.
- A heading is wrapped in its `Title` look. A chapter title is underlined with
  `═`, a section with `─`, both as wide as the title; a subsection is not.
- An item starts with `• ` and its next lines hang under its text; a nested
  item is indented two columns more.
- A table's columns are parted by ` │ `, and its header, in `Bold`, by a rule
  `─┼─`. A column is as wide as its widest cell. Wider than the screen, the
  widest column is narrowed a column at a time, the rightmost of equals first,
  down to its longest word; then down to its longest word holding code; then
  down to one. So text wraps before code is cut. Cells are padded to their
  column, a row is as high as its highest cell.
- An alert is a frame as wide as the screen, `┌ Note ─…┐` or `┌ Warning ─…┐`
  at the top, its prose laid out four columns narrower inside `│ … │`,
  parted by blank lines.
- A `LinkStop` is the link's `Index`, counted from 0 in reading order through
  the chapter, and its `Target`. The index is a deliberate `int`: it only
  tells the pieces of one link from those of the next, so a link wrapped over
  two lines, the space between its words included, is one stop.

`ChapterLayoutTests` proves each rule, a chapter of every block at 80, 120
and 40 columns, and that no line of the guide is wider than 40, 80 or 120.

## The help's state

`Ui/Help/HelpState` is pure, as `ViewerState` is. A `HelpPage` is a chapter,
the width it is laid out at, its lines, the first line on screen and the
chosen link; a `HelpState` is the page shown, the screen's height and the
pages to go back to, newest first.

- `fitted` lays the page out again only when the width changes. The line on
  top keeps its block on top, as far into it as before in proportion:
  line 3 of a block of 8 at 80 columns is line 5 of the same block of 14 at
  40. The top then stays within the lines, so a taller screen at the end
  keeps the last line at the bottom.
- The arrows, `PgUp`, `PgDn`, `Home` and `End` move as in the viewer, a page
  being a screenful.
- The chosen link is always on screen: a scroll that hides it drops it.
  `Tab` chooses the link after the chosen one, or with none chosen the first
  link on screen, or failing that the first below it; `Shift+Tab` mirrors it.
  A link off screen is scrolled to the nearest edge. On the last link `Tab`
  keeps it.
- `follow` takes the reader of chapters, `EmbeddedGuide.read` in the view.
  It opens the target chapter, or the page's own for an anchor of its own, at
  the anchor's heading or at the top, and pushes the page left, chosen link
  included. A chapter that cannot be read is the `GuideFault` it answers, and
  the view hands it to `Ports.Report`. Enter with no link chosen changes
  nothing.
- `Back` pops the newest page and lays it out at the current width.
- `title` is the chapter's first `#` heading, or its file name.

`Ui/Help/HelpColours.attributeOf` answers a piece's `Attribute` from
`ClassicScheme`: body text bright cyan, inline code bright yellow, headings
bold white, rules cyan, a note's frame white and a warning's bright red, all
on the panel's blue; a link bright green and underlined, the chosen link
black on cyan, the colour of a match in the viewer. `Bold` and `Italic` add
their `TextStyle`.

`HelpStateTests` proves scrolling, `Tab` and `Shift+Tab` on and off screen,
following a link to a chapter, to a heading of another chapter and to one of
its own, a chapter that cannot be read, going back once and twice, and the
resizes; `HelpColoursTests` each colour; `HelpViewTests` the screen's three
parts, the keys and that `Tab` is kept by the content. The
`render-tui-screen` probe ran the view under a real `Application` at 80 and
40 columns: `Tab` lit *Differences between platforms* black on cyan, `Enter`
opened `16-platforms.md`, `Backspace` came back with the link still chosen,
the narrower screen kept the same paragraph on top, and `Esc` closed the
help.

## Searching the help

`F7` opens the viewer's search popup, `ViewerSearchDialog`, with its options:
case, whole words, regular expression, backwards. The offer is
`HelpState.Typed` and the help's last query, so the popup always opens as a
text search. `Shift+F7` repeats the last query without the popup, or opens it
when there is none. `HelpState.typedIn` puts a text typed first in `Typed`,
once. The list lives as long as the help: the history file is not read or
written, so opening the help reaches neither the state nor a port, from a
panel, the viewer or a popup alike.

- The search matches the laid-out lines of the page shown, each line as the
  text of its pieces, with `WindowSearch.occurrenceFrom`. A phrase wrapped
  over two lines is not found, as a match never crosses a line break in the
  viewer. Table rules, bullets and alert frames are text of their lines and
  can match.
- `F7` searches from the screen: forwards from the start of its first line,
  backwards from the end of its last. `Shift+F7` searches from the match:
  forwards from its end, backwards from its start; without a match, from the
  screen.
- A match is a `FoundSpan` of the page, its line and columns. A match on
  screen leaves the screen; a match off it puts its line on top, as far as
  the end of the chapter lets it. A chosen link that this scroll hides is
  dropped, as on any scroll.
- Not found, the page is unchanged, the old match included, and the view
  reports `TextNotFound`; a regular expression that does not parse is
  `Refused`. Both are the viewer's `SearchReport`s and texts.
- `HelpState.LastQuery` belongs to the help, not to a page, so it outlasts a
  link followed and going back. The match belongs to the page: a link
  followed opens a page without one, so the next `Shift+F7` searches the new
  chapter from its screen; going back restores the page left, its match
  included.
- A change of width lays the page out again and drops the match, since its
  columns no longer hold; the next `Shift+F7` searches from the screen.
- The match is drawn over the pieces in `ClassicScheme.foundText`, black on
  cyan, as in the viewer. A chosen link has the same colour.

`HelpSearchTests` proves a match on and off screen, `Shift+F7` after a match
and without one, the end of the chapter, both directions from the screen and
from a match, a chosen link dropped by the scroll, the row a match is drawn
on, a link followed, going back and a resize. `HelpViewSearchTests` drives
`F7` and `Shift+F7` through the view over the real guide: the offer, the text
remembered, the match on screen, a miss and a broken expression reported. The
`render-tui-screen` probe ran the view with the real `ViewerSearchDialog`
under an `Application`: `F7` `chapter` drew *chapter* black on cyan, `zzz`
opened *'zzz' was not found.*, `Shift+F7` moved to the next *key*, and after
`Tab` and `Enter` it found the first *key* of `16-platforms.md`. The probe puts
the screen back to the console's size between keys; at another size each
change of width dropped the match, as the last rule says.

## Opening the help where the user stands

`KeyMap.helpKey`, `F1`, is the first row of `KeyMap.bindings`,
`HelpRequested`, labelled `Help` on the panels' bar; `ViewerKeys` binds it to
`HelpAsked`, labelled `Help` on the viewer's bar. A place in the guide is a
`GuidePlace` of `Guide/`: a `ChapterName` and the `Anchor` of a heading, or
none for the top. `GuidePlace.contents` is `README.md`.

Two tables name the places:

- `Update.helpPlaceOf` answers the place of a message `KeyMap.bindings`
  raises, the section whose key table holds its key, and `None` for any other
  message and for `HelpRequested`. It is in `State` because `Update.update`
  reads it.
- `Ui/Help/HelpPlaces` answers `viewer`, `11-viewer.md`, and `ofPopup`, the
  section of each `Popup`: the twenty popups that ask for something, the
  results window of `Alt+F7` among them. Popups are views, so this table is in
  `Ui`.

Both build a place through `GuidePlace`, which has one function per chapter of
the guide, taking a heading or none. A chapter name it cannot make falls back
to the contents. `HelpPlacesTests` fails on a binding's message without a
place, on a place that is the contents, and on a place
`ChapterLinks.unreached` does not find in the embedded guide.

**The last command.** `Update.update` ends with `placingHelp`, after
`watching`, `remembering` and `forgettingMarks`: a message `helpPlaceOf`
places sets `AppState.HelpPlace`, any other leaves it. The place is read from
the message inside `update`, so `Message` has no wrapper case, the UI
dispatches every message the same way, and `transition` and `LogEntry` see the
command as it is. Only the bindings send the placed messages, from a key
through `PanelWiring` and from a click through the bar; moving the cursor, the
quick search and `Enter` send none and change nothing. `HelpRequested` has no
place, so `F1` twice opens at the same section. A command that is refused or
has nothing to do moves the place too: the user asked about it.
`HelpRequested` answers `ShowHelp state.HelpPlace`. The state keeps the place
and not the message because `AppState.fs` is compiled before the `Message`
of `Update.fs`. `Ins` on a panel of 10 000 files took 150 µs before and after,
see [Measurements](../Progress/MEASUREMENTS.md).

**The adapter.** `ShowHelp` takes the place alone. `Prompts.showHelp` opens `HelpState.opening
EmbeddedGuide.read place` as a runnable of its own over whatever is open.
A page laid out the first time puts the heading it `OpensAt` on top; a place
other than the contents has the contents to go back to. A `GuideFault` is
reported with `HelpReport.describe`: an absent chapter by name, a refused one
with its line and the construct in words. `running`, the helper every popup
goes through, attaches `HelpOpening.onHelpKey` with the popup's place: `F1`
reaches the popup's `KeyDown` when the view holding the focus leaves it, as a
text field and a list do. The viewer handles `F1` itself and calls its
`ShowHelp` port, so it runs without the hook, and so does the help's own
search popup. Message boxes are built inside `MessageBox.Query` and the
progress window by `BatchRunner`; neither opens the help.

`HelpPlacesTests`, `UpdateHelpTests`, `ShellHelpTests`, `HelpReportTests`,
`HelpOpeningTests` and `HelpKeyTests` prove the table, the last command, the
effect, the texts, the hook on a popup with a focused field, and `F1` through
`MainWindow` before a command, after one, after a cursor move and a quick
search. The probe ran the application with its real dependencies at 120 by 30
columns: `F1` in a panel opened the contents, after `Ctrl+R` *Reading the
panel again*, also after `↓`; after `F3` the viewer's chapter; over `F7`
*Making directories and files*, over `F12` *Setup*, over `Alt+F12` *The
history popup*; and `Esc` closed the help and left *Create directory* open.

## The view is Marbas's own

Terminal.Gui 2.4.17 has a `Markdown` view. A spike on 2026-10-21 rendered a
chapter in it at 40, 80 and 120 columns. It walks links with `Tab` and
`Shift+Tab`, raises `LinkClicked` on `Enter` and scrolls to a heading by
GitHub's anchor, `notes-1` for a second `Notes` included. Five things rule it
out:

- A table's height does not follow its wrapped rows: at 80 columns its last
  rows are missing, and at 120 the bullets under it are blank until the view
  scrolls. At 40 columns words break inside, `Alternativ` / `e`.
- `[!NOTE]` and `[!WARNING]` both draw as a plain faint quote, with Markdig's
  alert extension too.
- A link wrapped over two lines is two `Tab` stops, each lighting half of it.
- Inline code has a fixed grey background, poorly legible on the blue panel.
- Nothing selects a range of text, so `F7` could not show a match.

The help lays out the chapter itself, as
[the plan](../Plan/PHASE-24-THE-GUIDE-AND-THE-HELP.md) decided. Markdig 1.3.2,
which Terminal.Gui already depends on, parses it, and its anchors are the ones
the spike saw `ScrollToAnchor` resolve.
