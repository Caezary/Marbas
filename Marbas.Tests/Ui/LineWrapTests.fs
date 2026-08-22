module Marbas.Tests.Ui.LineWrapTests

open Xunit
open Marbas.Ui

[<Fact>]
let ``a line that fits is one segment`` () =
    let segments = LineWrap.segments 10 "short"

    Assert.Equal<string list>([ "short" ], segments)

[<Fact>]
let ``a long line is cut every width`` () =
    let segments = LineWrap.segments 3 "abcdefgh"

    Assert.Equal<string list>([ "abc"; "def"; "gh" ], segments)

[<Fact>]
let ``an empty line is still one segment`` () =
    let segments = LineWrap.segments 5 ""

    Assert.Equal<string list>([ "" ], segments)

[<Fact>]
let ``a width of nothing wraps nothing`` () =
    let segments = LineWrap.segments 0 "abc"

    Assert.Equal<string list>([ "abc" ], segments)

[<Fact>]
let ``the height of a line is the segments it takes`` () =
    let height = LineWrap.height 3 "abcdefgh"

    Assert.Equal(3, height)

[<Fact>]
let ``an unwrapped line shows the window it was asked for`` () =
    let shown = LineWrap.visible 4 2 "abcdefgh"

    Assert.Equal("cdef", shown)

[<Fact>]
let ``an unwrapped line stops at its end`` () =
    let shown = LineWrap.visible 40 6 "abcdefgh"

    Assert.Equal("gh", shown)

[<Fact>]
let ``scrolled past its end a line shows nothing`` () =
    let shown = LineWrap.visible 4 40 "abcdefgh"

    Assert.Equal("", shown)
