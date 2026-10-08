module Marbas.Tests.Ui.Viewer.FoundSpanTests

open Xunit
open Marbas.Ui.Viewer

let private span line from until : FoundSpan =
    { Line = line
      From = from
      Until = until }

let private row line from text : ShownRow =
    { Line = line
      From = from
      Text = text }

[<Fact>]
let ``a match in plain text covers the columns it was found at`` () =
    let found = FoundSpan.ofMatch 4 "alpha beta gamma" 6 4

    Assert.Equal(span 4 6 10, found)

[<Fact>]
let ``a tab before the match pushes it to the columns it is shown at`` () =
    let found = FoundSpan.ofMatch 0 "ab\tneedle" 3 6

    Assert.Equal(span 0 8 14, found)

[<Fact>]
let ``a tab inside the match widens it to the stop it runs to`` () =
    let found = FoundSpan.ofMatch 0 "ab\tc" 1 3

    Assert.Equal(span 0 1 9, found)

[<Theory>]
[<InlineData(0, "alpha beta gamma", 6, 10, 6, 4)>]
[<InlineData(4, "ha beta gamma", 6, 10, 2, 4)>]
[<InlineData(8, "eta gamma", 6, 10, 0, 2)>]
[<InlineData(0, "alpha be", 6, 10, 6, 2)>]
[<InlineData(0, "alpha beta gamma", 0, 16, 0, 16)>]
let ``a row showing part of the match marks that part`` from (text: string) spanFrom spanUntil start length =
    let shown = row 2 from text

    let marked = FoundSpan.onRow shown (span 2 spanFrom spanUntil)

    Assert.Equal(Some(start, length), marked)

[<Theory>]
[<InlineData(2, 0, "alpha")>]
[<InlineData(2, 11, "gamma")>]
[<InlineData(3, 0, "alpha beta gamma")>]
[<InlineData(2, 10, "")>]
let ``a row showing none of the match marks nothing`` line from (text: string) =
    let shown = row line from text

    let marked = FoundSpan.onRow shown (span 2 6 10)

    Assert.Equal(None, marked)

[<Theory>]
[<InlineData(0, 6, 10, 0)>]
[<InlineData(4, 6, 10, 4)>]
[<InlineData(0, 76, 84, 4)>]
[<InlineData(0, 100, 104, 24)>]
[<InlineData(50, 10, 14, 10)>]
[<InlineData(0, 90, 200, 90)>]
let ``the screen moves across only as far as it takes to show the match`` left from until expected =
    let moved = FoundSpan.leftShowing left 80 (span 0 from until)

    Assert.Equal(expected, moved)
