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
  [the test of the keys](#the-guide-and-the-key-maps). `F1` is absent until
  P24.8 binds it.
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
`ViewerKeys.keys` and `FindKeys.keys`. It compares them with the whole guide,
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
`width` columns wide. A `LaidLine` is a list of `Piece`s and the `Anchor` of
the heading it starts, if it starts one. A `Piece` is text with a `Look` —
`Plain`, `Literal` for inline code, `Title` of a heading level, `Ruling` for
the rules of a table, `Framing` of an alert kind — a set of `Stress`, `Bold`
and `Italic`, and the `LinkStop` it belongs to. Adjacent pieces of one style
are merged. A column is one character: the guide uses no wide characters.

- Blocks are parted by one blank line. A paragraph is reflowed: words, split
  at spaces, fill a line greedily. Inline code is never split at its own
  spaces. A word wider than the line is cut at the width.
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
