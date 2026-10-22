module Marbas.Tests.Ui.Help.HelpReportTests

open Microsoft.FSharp.Reflection
open Xunit
open Marbas.Guide
open Marbas.Ui.Help
open Marbas.Tests

let private name raw =
    ChapterName.tryCreate raw |> Expect.some

let private constructNamed raw =
    FSharpType.GetUnionCases typeof<Construct>
    |> Array.find (fun case -> case.Name = raw)
    |> fun case -> FSharpValue.MakeUnion(case, [||]) :?> Construct

[<Fact>]
let ``a chapter missing from the binary is named`` () =
    let text = HelpReport.describe (Absent(name "02-panels.md"))

    Assert.Equal("The guide built into Marbas has no chapter 02-panels.md.", text)

[<Fact>]
let ``a chapter refused names the line and what is on it`` () =
    let refusal = { Line = 12; Construct = CodeBlock }

    let text = HelpReport.describe (Refused(name "02-panels.md", refusal))

    Assert.Equal("02-panels.md cannot be shown: line 12 holds a code block, which the help does not draw.", text)

[<Theory>]
[<InlineData("DeepHeading", "a heading below ###")>]
[<InlineData("OrderedList", "a numbered list")>]
[<InlineData("CodeBlock", "a code block")>]
[<InlineData("Quote", "a quote")>]
[<InlineData("AlertOfOtherKind", "an alert other than a note or a warning")>]
[<InlineData("AlertContent", "something other than paragraphs and lists inside an alert")>]
[<InlineData("ItemContent", "something other than a paragraph and one nested list inside an item")>]
[<InlineData("ThematicBreak", "a horizontal rule")>]
[<InlineData("Html", "HTML")>]
[<InlineData("Image", "an image")>]
[<InlineData("HardLineBreak", "a hard line break")>]
[<InlineData("ColumnAlignment", "an aligned table column")>]
[<InlineData("LinkOutsideTheGuide", "a link outside the guide")>]
[<InlineData("LinkReference", "a link reference definition")>]
let ``every construct the help refuses has its words`` (construct: string, words: string) =
    let text = HelpReport.construct (constructNamed construct)

    Assert.Equal(words, text)

[<Fact>]
let ``a construct the parser does not name is called by Markdig's name`` () =
    let text = HelpReport.construct (Unrecognised "FootnoteGroup")

    Assert.Equal("Markdown the help does not know (FootnoteGroup)", text)
