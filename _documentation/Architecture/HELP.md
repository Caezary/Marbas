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
  `KeyMap.bindings`, so the test of P24.4 needs them as a named list. `F1` is
  absent until P24.8 binds it.
- A link leads only to a chapter or a heading of the guide, since the parser
  refuses anything else; the releases page is named in words. The format of
  `user-menu.conf` is a table of lines, since a code block is refused.

`Marbas.fsproj` embeds the chapters as `user-guide/<file>`, shown in the
project under `UserGuide`; nothing reads them yet. `UserGuideTests` holds the
guide through those resources: it fails on a file missing from or added to the
seventeen it names, on a chapter the parser refuses, and on any link
`ChapterLinks.broken` reports. That function takes every `Chapter`, a
`ChapterName` and its blocks, gathers each link from headings, paragraphs,
items at any depth, table cells and alerts, and answers the links whose
chapter is not among them or whose anchor is no heading of that chapter.

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
