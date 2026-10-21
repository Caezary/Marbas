module Marbas.Tests.Guide.ChapterMarkdownTests

open Xunit
open Marbas.Guide
open Marbas.Tests

let private blocksOf text = ChapterMarkdown.parse text |> Expect.ok

let private refusalOf text =
    ChapterMarkdown.parse text |> Expect.error

let private headingOf block =
    match block with
    | Heading heading -> heading
    | other -> failwith $"expected a heading, got %A{other}"

let private plain text = Phrase [ Text text ]

let private paragraph spans = Prose(Paragraph(Phrase spans))

let private item text items = { Phrase = plain text; Items = items }

let private cell text = Cell(plain text)

let private empty = Cell(Phrase [])

let private chapter name =
    ChapterName.tryCreate name |> Expect.some

[<Fact>]
let ``no text is no blocks`` () =
    let blocks = blocksOf ""

    Assert.Empty blocks

[<Fact>]
let ``a paragraph is the phrase it reads`` () =
    let blocks = blocksOf "Marked entries are yellow."

    Assert.Equal<Block list>([ paragraph [ Text "Marked entries are yellow." ] ], blocks)

[<Fact>]
let ``the lines of a paragraph join with a space`` () =
    let blocks = blocksOf "Marked entries\nare yellow."

    Assert.Equal<Block list>([ paragraph [ Text "Marked entries are yellow." ] ], blocks)

[<Fact>]
let ``spans nest inside strong and emphasis`` () =
    let blocks = blocksOf "**bold *nested* `code`**"

    let expected =
        paragraph [ Strong(Phrase [ Text "bold "; Emphasis(plain "nested"); Text " "; Code "code" ]) ]

    Assert.Equal<Block list>([ expected ], blocks)

[<Fact>]
let ``underscores mark emphasis as asterisks do`` () =
    let blocks = blocksOf "_under_ and __over__"

    Assert.Equal<Block list>([ paragraph [ Emphasis(plain "under"); Text " and "; Strong(plain "over") ] ], blocks)

[<Fact>]
let ``an underscore inside a word is text`` () =
    let blocks = blocksOf "snake_case_word"

    Assert.Equal<Block list>([ paragraph [ Text "snake_case_word" ] ], blocks)

[<Fact>]
let ``an escaped character is text`` () =
    let blocks = blocksOf @"a \* b \_ c"

    Assert.Equal<Block list>([ paragraph [ Text "a * b _ c" ] ], blocks)

[<Fact>]
let ``an entity is the character it names`` () =
    let blocks = blocksOf "copy &amp; move"

    Assert.Equal<Block list>([ paragraph [ Text "copy & move" ] ], blocks)

[<Fact>]
let ``a link to a heading targets the same chapter`` () =
    let blocks = blocksOf "Read [the notes](#notes)."

    let expected =
        paragraph
            [ Text "Read "
              Link(plain "the notes", SameChapter(Anchor.create "notes"))
              Text "." ]

    Assert.Equal<Block list>([ expected ], blocks)

[<Fact>]
let ``a link to a chapter targets its start`` () =
    let blocks = blocksOf "[Marking](03-marking.md)"

    let expected =
        paragraph [ Link(plain "Marking", OtherChapter(chapter "03-marking.md", None)) ]

    Assert.Equal<Block list>([ expected ], blocks)

[<Fact>]
let ``a link to a heading of another chapter targets both`` () =
    let blocks = blocksOf "[Copy](05-file-operations.md#copy-and-move)"

    let target =
        OtherChapter(chapter "05-file-operations.md", Some(Anchor.create "copy-and-move"))

    Assert.Equal<Block list>([ paragraph [ Link(plain "Copy", target) ] ], blocks)

[<Fact>]
let ``the text of a link is a phrase`` () =
    let blocks = blocksOf "[the `Alt+E` key](#marking)"

    let text = Phrase [ Text "the "; Code "Alt+E"; Text " key" ]

    Assert.Equal<Block list>([ paragraph [ Link(text, SameChapter(Anchor.create "marking")) ] ], blocks)

[<Theory>]
[<InlineData("[site](https://example.com)")>]
[<InlineData("[readme](../README.md)")>]
[<InlineData("[nested](guide/03-marking.md)")>]
[<InlineData("[drive](C:03-marking.md)")>]
[<InlineData("[text](notes.txt)")>]
[<InlineData("[nothing](#)")>]
[<InlineData("[no anchor](03-marking.md#)")>]
[<InlineData("<https://example.com>")>]
let ``a link outside the guide is refused`` (text: string) =
    let refusal = refusalOf text

    Assert.Equal(LinkOutsideTheGuide, refusal.Construct)

[<Fact>]
let ``headings are a chapter's title, a section and a subsection`` () =
    let blocks = blocksOf "# Marking\n\n## By pattern\n\n### Undo"

    let levels = blocks |> List.map (headingOf >> _.Level)

    Assert.Equal<HeadingLevel list>([ ChapterTitle; Section; Subsection ], levels)

[<Theory>]
[<InlineData("# Marking", "marking")>]
[<InlineData("## Copy and move", "copy-and-move")>]
[<InlineData("## Sizes, links & the rest!", "sizes-links--the-rest")>]
[<InlineData("### `Alt+E` and `Alt+Shift+E`", "alte-and-altshifte")>]
[<InlineData("## Plain *and* bold", "plain-and-bold")>]
let ``a heading's anchor is the one GitHub gives it`` (text: string, expected: string) =
    let heading = blocksOf text |> List.exactlyOne |> headingOf

    Assert.Equal(expected, Anchor.value heading.Anchor)

[<Fact>]
let ``a heading repeated takes a numbered anchor`` () =
    let blocks = blocksOf "## Notes\n\n## Notes\n\n## Notes"

    let anchors = blocks |> List.map (headingOf >> _.Anchor >> Anchor.value)

    Assert.Equal<string list>([ "notes"; "notes-1"; "notes-2" ], anchors)

[<Fact>]
let ``a heading's title is a phrase`` () =
    let heading = blocksOf "## The `F1` key" |> List.exactlyOne |> headingOf

    Assert.Equal(Phrase [ Text "The "; Code "F1"; Text " key" ], heading.Title)

[<Fact>]
let ``a bullet list is its items`` () =
    let blocks = blocksOf "- one\n- two"

    Assert.Equal<Block list>([ Prose(Bullets [ item "one" []; item "two" [] ]) ], blocks)

[<Fact>]
let ``an item holds the items nested under it`` () =
    let blocks = blocksOf "- one\n  - nested\n  - again\n- two"

    let expected =
        Prose(Bullets [ item "one" [ item "nested" []; item "again" [] ]; item "two" [] ])

    Assert.Equal<Block list>([ expected ], blocks)

[<Fact>]
let ``a table is its header and its rows`` () =
    let blocks =
        blocksOf "| Key | Action |\n|-----|--------|\n| `Ins` | Mark |\n| `*` | Invert |"

    let expected =
        Table.create
            (Row [ cell "Key"; cell "Action" ])
            [ Row [ Cell(Phrase [ Code "Ins" ]); cell "Mark" ]
              Row [ Cell(Phrase [ Code "*" ]); cell "Invert" ] ]

    Assert.Equal<Block list>([ Table expected ], blocks)

[<Fact>]
let ``an empty cell is an empty phrase`` () =
    let blocks =
        blocksOf "| Key | Alternative | Action |\n|---|---|---|\n| `*` | | Invert |"

    let expected =
        Table.create
            (Row [ cell "Key"; cell "Alternative"; cell "Action" ])
            [ Row [ Cell(Phrase [ Code "*" ]); empty; cell "Invert" ] ]

    Assert.Equal<Block list>([ Table expected ], blocks)

[<Fact>]
let ``a row of another width is fitted to the header`` () =
    let blocks = blocksOf "| A | B |\n|---|---|\n| a | b | c |\n| only |"

    let expected =
        Table.create (Row [ cell "A"; cell "B" ]) [ Row [ cell "a"; cell "b" ]; Row [ cell "only"; empty ] ]

    Assert.Equal<Block list>([ Table expected ], blocks)

[<Fact>]
let ``an escaped bar is text inside a cell`` () =
    let blocks = blocksOf "| Action |\n|---|\n| Mark \\| unmark |"

    let expected = Table.create (Row [ cell "Action" ]) [ Row [ cell "Mark | unmark" ] ]

    Assert.Equal<Block list>([ Table expected ], blocks)

[<Fact>]
let ``a note is an alert of its prose`` () =
    let blocks = blocksOf "> [!NOTE]\n> The grey `+`."

    let expected =
        Alert(Note, [ Paragraph(Phrase [ Text "The grey "; Code "+"; Text "." ]) ])

    Assert.Equal<Block list>([ expected ], blocks)

[<Fact>]
let ``a warning holds paragraphs and bullets`` () =
    let blocks = blocksOf "> [!WARNING]\n> Wipes for good.\n>\n> - no undo"

    let expected =
        Alert(Warning, [ Paragraph(plain "Wipes for good."); Bullets [ item "no undo" [] ] ])

    Assert.Equal<Block list>([ expected ], blocks)

[<Theory>]
[<InlineData("#### Deep")>]
[<InlineData("###### Deeper")>]
let ``a heading below the third level is refused`` (text: string) =
    let refusal = refusalOf text

    Assert.Equal(DeepHeading, refusal.Construct)

[<Theory>]
[<InlineData("1. one")>]
[<InlineData("- one\n  1. nested")>]
[<InlineData("> [!NOTE]\n> 1. one")>]
let ``an ordered list is refused`` (text: string) =
    let refusal = refusalOf text

    Assert.Equal(OrderedList, refusal.Construct)

[<Theory>]
[<InlineData("```\ncode\n```")>]
[<InlineData("    indented")>]
let ``a code block is refused`` (text: string) =
    let refusal = refusalOf text

    Assert.Equal(CodeBlock, refusal.Construct)

[<Fact>]
let ``a quote that is no alert is refused`` () =
    let refusal = refusalOf "> plain"

    Assert.Equal(Quote, refusal.Construct)

[<Theory>]
[<InlineData("> [!TIP]\n> Try.")>]
[<InlineData("> [!IMPORTANT]\n> Read.")>]
[<InlineData("> [!CAUTION]\n> Mind.")>]
let ``an alert of another kind is refused`` (text: string) =
    let refusal = refusalOf text

    Assert.Equal(AlertOfOtherKind, refusal.Construct)

[<Theory>]
[<InlineData("> [!NOTE]\n> ## Heading")>]
[<InlineData("> [!NOTE]\n> | A |\n> |---|\n> | a |")>]
let ``an alert holding more than prose is refused`` (text: string) =
    let refusal = refusalOf text

    Assert.Equal(AlertContent, refusal.Construct)

[<Theory>]
[<InlineData("- one\n\n  second paragraph")>]
[<InlineData("- one\n\n  | A |\n  |---|\n  | a |")>]
let ``an item holding more than a phrase and its items is refused`` (text: string) =
    let refusal = refusalOf text

    Assert.Equal(ItemContent, refusal.Construct)

[<Theory>]
[<InlineData("---")>]
[<InlineData("***")>]
let ``a thematic break is refused`` (text: string) =
    let refusal = refusalOf text

    Assert.Equal(ThematicBreak, refusal.Construct)

[<Theory>]
[<InlineData("<div>block</div>")>]
[<InlineData("an <b>inline</b> tag")>]
let ``html is refused`` (text: string) =
    let refusal = refusalOf text

    Assert.Equal(Html, refusal.Construct)

[<Fact>]
let ``an image is refused`` () =
    let refusal = refusalOf "![panel](panel.png)"

    Assert.Equal(Image, refusal.Construct)

[<Theory>]
[<InlineData("one  \ntwo")>]
[<InlineData("one\\\ntwo")>]
let ``a hard line break is refused`` (text: string) =
    let refusal = refusalOf text

    Assert.Equal(HardLineBreak, refusal.Construct)

[<Theory>]
[<InlineData("| A |\n|:--|\n| a |")>]
[<InlineData("| A |\n|:-:|\n| a |")>]
[<InlineData("| A |\n|--:|\n| a |")>]
let ``an aligned column is refused`` (text: string) =
    let refusal = refusalOf text

    Assert.Equal(ColumnAlignment, refusal.Construct)

[<Fact>]
let ``a link reference definition is refused`` () =
    let refusal = refusalOf "[Marking][m]\n\n[m]: 03-marking.md"

    Assert.Equal(LinkReference, refusal.Construct)

[<Fact>]
let ``a refused block names its line, counted from one`` () =
    let refusal = refusalOf "# Marking\n\nMarked.\n\n---"

    Assert.Equal(5, refusal.Line)

[<Fact>]
let ``a refused span names its own line`` () =
    let refusal = refusalOf "Marked\nentries ![panel](panel.png)"

    Assert.Equal(2, refusal.Line)

[<Fact>]
let ``the first refusal in the text is the one answered`` () =
    let refusal = refusalOf "Marked.\n\n#### Deep\n\n---"

    Assert.Equal({ Line = 3; Construct = DeepHeading }, refusal)
