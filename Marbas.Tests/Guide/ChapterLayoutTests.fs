module Marbas.Tests.Guide.ChapterLayoutTests

open Xunit
open Marbas.Guide
open Marbas.Tests

let private blocksOf text = ChapterMarkdown.parse text |> Expect.ok

let private textOf (line: LaidLine) =
    line.Pieces |> List.map _.Text |> String.concat ""

let private textsAt width text =
    ChapterLayout.lay width (blocksOf text) |> List.map textOf

let private piecesAt width text =
    ChapterLayout.lay width (blocksOf text) |> List.map _.Pieces

let private piece look text =
    { Text = text
      Look = look
      Stress = Set.empty
      Link = None }

let private stressed stress text =
    { piece Plain text with
        Stress = set [ stress ] }

let private keyTable =
    "| Key | Alternative | Action |\n|-----|-------------|--------|\n"

[<Fact>]
let ``a paragraph is reflowed to the width`` () =
    let text = "one two three four five six"

    let lines = textsAt 10 text

    Assert.Equal<string list>([ "one two"; "three four"; "five six" ], lines)

[<Fact>]
let ``a word wider than the line is cut at the width`` () =
    let text = "abcdefghijkl"

    let lines = textsAt 5 text

    Assert.Equal<string list>([ "abcde"; "fghij"; "kl" ], lines)

[<Fact>]
let ``inline code is a piece of its own look`` () =
    let text = "Press `F5` now"

    let lines = piecesAt 80 text

    Assert.Equal<Piece list list>([ [ piece Plain "Press "; piece Literal "F5"; piece Plain " now" ] ], lines)

[<Fact>]
let ``inline code is not broken at its spaces`` () =
    let text = "aa `edit %p` bb"

    let lines = textsAt 9 text

    Assert.Equal<string list>([ "aa"; "edit %p"; "bb" ], lines)

[<Fact>]
let ``strong and emphasised text carry their stress`` () =
    let text = "**bold** and *slanted*"

    let lines = piecesAt 80 text

    Assert.Equal<Piece list list>([ [ stressed Bold "bold"; piece Plain " and "; stressed Italic "slanted" ] ], lines)

[<Fact>]
let ``a link wrapped over two lines is one stop on both`` () =
    let text = "[one two three](02-panels.md)"
    let chapter = ChapterName.tryCreate "02-panels.md" |> Expect.some

    let stop =
        Some
            { Index = 0
              Target = OtherChapter(chapter, None) }

    let lines = piecesAt 8 text

    Assert.Equal<Piece list list>(
        [ [ { piece Plain "one two" with
                Link = stop } ]
          [ { piece Plain "three" with Link = stop } ] ],
        lines
    )

[<Fact>]
let ``the links of a chapter are numbered in reading order`` () =
    let text =
        "# Links\n\nSee [one](#links).\n\n- and [two](#links)\n\n"
        + keyTable
        + "| [three](#links) | | x |\n"

    let indexes =
        ChapterLayout.lay 80 (blocksOf text)
        |> List.collect _.Pieces
        |> List.choose _.Link
        |> List.map _.Index

    Assert.Equal<int list>([ 0; 1; 2 ], indexes)

[<Fact>]
let ``the first line of a heading carries its anchor`` () =
    let text = "## Two words that wrap"

    let anchors = ChapterLayout.lay 10 (blocksOf text) |> List.map _.Anchor

    Assert.Equal<Anchor option list>([ Some(Anchor.create "two-words-that-wrap"); None; None ], anchors)

[<Fact>]
let ``a chapter's title is underlined twice`` () =
    let text = "# Panels"

    let lines = textsAt 80 text

    Assert.Equal<string list>([ "Panels"; "══════" ], lines)

[<Fact>]
let ``a section is underlined once`` () =
    let text = "## Sorting"

    let lines = textsAt 80 text

    Assert.Equal<string list>([ "Sorting"; "───────" ], lines)

[<Fact>]
let ``a subsection is not underlined`` () =
    let text = "### Notes"

    let lines = textsAt 80 text

    Assert.Equal<string list>([ "Notes" ], lines)

[<Fact>]
let ``a heading takes the look of its level`` () =
    let text = "## Sorting"

    let lines = piecesAt 80 text

    Assert.Equal<Piece list list>([ [ piece (Title Section) "Sorting" ]; [ piece (Title Section) "───────" ] ], lines)

[<Fact>]
let ``blocks are parted by a blank line`` () =
    let text = "One.\n\nTwo."

    let lines = textsAt 80 text

    Assert.Equal<string list>([ "One."; ""; "Two." ], lines)

[<Fact>]
let ``an item hangs its next lines under its text`` () =
    let text = "- one two three"

    let lines = textsAt 9 text

    Assert.Equal<string list>([ "• one two"; "  three" ], lines)

[<Fact>]
let ``a nested item is indented under its parent`` () =
    let text = "- one\n  - two three\n- four"

    let lines = textsAt 9 text

    Assert.Equal<string list>([ "• one"; "  • two"; "    three"; "• four" ], lines)

[<Fact>]
let ``a table sizes its columns to their content`` () =
    let text = keyTable + "| `F5` | | Copy |\n"

    let lines = textsAt 80 text

    Assert.Equal<string list>(
        [ "Key │ Alternative │ Action"
          "────┼─────────────┼───────"
          "F5  │             │ Copy  " ],
        lines
    )

[<Fact>]
let ``a table wider than the line wraps inside its widest column`` () =
    let text = keyTable + "| `F5` | | Copy into the other panel |\n"

    let lines = textsAt 30 text

    Assert.Equal<string list>(
        [ "Key │ Alternative │ Action    "
          "────┼─────────────┼───────────"
          "F5  │             │ Copy into "
          "    │             │ the other "
          "    │             │ panel     " ],
        lines
    )

[<Fact>]
let ``a table too narrow for its longest words cuts text before code`` () =
    let text =
        "| Key | Kept by |\n|-----|---------|\n| `Alt+Shift+F11` | gnome-terminal under WSLg |\n"

    let lines = textsAt 24 text

    Assert.Equal<string list>(
        [ "Key           │ Kept by "
          "──────────────┼─────────"
          "Alt+Shift+F11 │ gnome-te"
          "              │ rminal  "
          "              │ under   "
          "              │ WSLg    " ],
        lines
    )

[<Fact>]
let ``a table's header is bold and its rules are of their own look`` () =
    let text = "| Key | Action |\n|-----|--------|\n| `F5` | Copy |\n"

    let lines = piecesAt 80 text

    Assert.Equal<Piece list list>(
        [ [ stressed Bold "Key"; piece Ruling " │ "; stressed Bold "Action" ]
          [ piece Ruling "────┼───────" ]
          [ piece Literal "F5"
            piece Plain " "
            piece Ruling " │ "
            piece Plain "Copy  " ] ],
        lines
    )

[<Fact>]
let ``an alert is framed under the name of its kind`` () =
    let text = "> [!NOTE]\n> Mind it."

    let lines = textsAt 20 text

    Assert.Equal<string list>([ "┌ Note ────────────┐"; "│ Mind it.         │"; "└──────────────────┘" ], lines)

[<Fact>]
let ``the prose of an alert is parted by a blank line inside the frame`` () =
    let text = "> [!WARNING]\n> One.\n>\n> - two"

    let lines = textsAt 20 text

    Assert.Equal<string list>(
        [ "┌ Warning ─────────┐"
          "│ One.             │"
          "│                  │"
          "│ • two            │"
          "└──────────────────┘" ],
        lines
    )

[<Fact>]
let ``the frame of an alert takes the look of its kind`` () =
    let text = "> [!WARNING]\n> Mind it."

    let lines = piecesAt 20 text

    Assert.Equal<Piece list list>(
        [ [ piece (Framing Warning) "┌ Warning ─────────┐" ]
          [ piece (Framing Warning) "│ "
            piece Plain "Mind it.        "
            piece (Framing Warning) " │" ]
          [ piece (Framing Warning) "└──────────────────┘" ] ],
        lines
    )

let private sample =
    "# Copying\n\n"
    + "Press `F5` to **copy** the *marked* entries into the directory of the other panel, as [Panels](02-panels.md#sizes) says.\n\n"
    + "## Keys\n\n"
    + keyTable
    + "| `F5` | | Copy into the other panel's directory |\n"
    + "| `Alt+Shift+F11` | `Alt+Shift+Z` | Close the tab in front and go to the one behind it |\n\n"
    + "- A copy keeps the dates of the entries it copies.\n"
    + "  - A link is copied as a link, never as what it leads to.\n"
    + "- `Esc` stops at the next entry.\n\n"
    + "### Platforms\n\n"
    + "> [!NOTE]\n> Windows Terminal keeps `F11` for full screen; press `Alt+T`.\n\n"
    + "> [!WARNING]\n> A copy onto itself is refused.\n"

[<Fact>]
let ``a chapter of every block is laid out at 80 columns`` () =
    let text = sample

    let lines = textsAt 80 text

    Assert.Equal<string list>(
        [ "Copying"
          "═══════"
          ""
          "Press F5 to copy the marked entries into the directory of the other panel, as"
          "Panels says."
          ""
          "Keys"
          "────"
          ""
          "Key           │ Alternative │ Action                                            "
          "──────────────┼─────────────┼───────────────────────────────────────────────────"
          "F5            │             │ Copy into the other panel's directory             "
          "Alt+Shift+F11 │ Alt+Shift+Z │ Close the tab in front and go to the one behind it"
          ""
          "• A copy keeps the dates of the entries it copies."
          "  • A link is copied as a link, never as what it leads to."
          "• Esc stops at the next entry."
          ""
          "Platforms"
          ""
          "┌ Note ────────────────────────────────────────────────────────────────────────┐"
          "│ Windows Terminal keeps F11 for full screen; press Alt+T.                     │"
          "└──────────────────────────────────────────────────────────────────────────────┘"
          ""
          "┌ Warning ─────────────────────────────────────────────────────────────────────┐"
          "│ A copy onto itself is refused.                                               │"
          "└──────────────────────────────────────────────────────────────────────────────┘" ],
        lines
    )

[<Fact>]
let ``a chapter of every block is laid out at 120 columns`` () =
    let text = sample

    let lines = textsAt 120 text

    Assert.Equal<string list>(
        [ "Copying"
          "═══════"
          ""
          "Press F5 to copy the marked entries into the directory of the other panel, as Panels says."
          ""
          "Keys"
          "────"
          ""
          "Key           │ Alternative │ Action                                            "
          "──────────────┼─────────────┼───────────────────────────────────────────────────"
          "F5            │             │ Copy into the other panel's directory             "
          "Alt+Shift+F11 │ Alt+Shift+Z │ Close the tab in front and go to the one behind it"
          ""
          "• A copy keeps the dates of the entries it copies."
          "  • A link is copied as a link, never as what it leads to."
          "• Esc stops at the next entry."
          ""
          "Platforms"
          ""
          "┌ Note ────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐"
          "│ Windows Terminal keeps F11 for full screen; press Alt+T.                                                             │"
          "└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘"
          ""
          "┌ Warning ─────────────────────────────────────────────────────────────────────────────────────────────────────────────┐"
          "│ A copy onto itself is refused.                                                                                       │"
          "└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘" ],
        lines
    )

[<Fact>]
let ``a chapter of every block is laid out at 40 columns`` () =
    let text = sample

    let lines = textsAt 40 text

    Assert.Equal<string list>(
        [ "Copying"
          "═══════"
          ""
          "Press F5 to copy the marked entries into"
          "the directory of the other panel, as"
          "Panels says."
          ""
          "Keys"
          "────"
          ""
          "Key           │ Alternative │ Action    "
          "──────────────┼─────────────┼───────────"
          "F5            │             │ Copy into "
          "              │             │ the other "
          "              │             │ panel's   "
          "              │             │ directory "
          "Alt+Shift+F11 │ Alt+Shift+Z │ Close the "
          "              │             │ tab in    "
          "              │             │ front and "
          "              │             │ go to the "
          "              │             │ one behind"
          "              │             │ it        "
          ""
          "• A copy keeps the dates of the entries"
          "  it copies."
          "  • A link is copied as a link, never as"
          "    what it leads to."
          "• Esc stops at the next entry."
          ""
          "Platforms"
          ""
          "┌ Note ────────────────────────────────┐"
          "│ Windows Terminal keeps F11 for full  │"
          "│ screen; press Alt+T.                 │"
          "└──────────────────────────────────────┘"
          ""
          "┌ Warning ─────────────────────────────┐"
          "│ A copy onto itself is refused.       │"
          "└──────────────────────────────────────┘" ],
        lines
    )

[<Theory>]
[<InlineData(40)>]
[<InlineData(80)>]
[<InlineData(120)>]
let ``no line of the guide is wider than the screen`` (width: int) =
    let guide = UserGuide.chapters ()

    let wider =
        guide
        |> List.collect (fun chapter -> ChapterLayout.lay width chapter.Contents)
        |> List.map textOf
        |> List.filter (fun line -> line.Length > width)

    Assert.Empty wider
