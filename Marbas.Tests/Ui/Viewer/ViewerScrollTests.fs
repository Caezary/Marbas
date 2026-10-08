module Marbas.Tests.Ui.Viewer.ViewerScrollTests

open Xunit
open Marbas.Ui.Viewer

[<Fact>]
let ``a step forward moves one on`` () =
    let top = ViewerScroll.moved 100 40 1

    Assert.Equal(41, top)

[<Fact>]
let ``a step back never goes before the start`` () =
    let top = ViewerScroll.moved 100 0 -1

    Assert.Equal(0, top)

[<Fact>]
let ``a step on never goes past the bound`` () =
    let top = ViewerScroll.moved 12 12 8

    Assert.Equal(12, top)

[<Fact>]
let ``a bound of nothing keeps everything at the start`` () =
    let top = ViewerScroll.moved 0 0 5

    Assert.Equal(0, top)

[<Fact>]
let ``a page of single rows holds as many lines as it is tall`` () =
    let lines = ViewerScroll.filling 20 (Seq.replicate 40 1)

    Assert.Equal(20, lines)

[<Fact>]
let ``wrapped lines fill a page sooner`` () =
    let lines =
        ViewerScroll.filling
            10
            (seq {
                4
                3
                2
                6
                1
            })

    Assert.Equal(3, lines)

[<Fact>]
let ``a line taller than the page is still a page`` () =
    let lines =
        ViewerScroll.filling
            5
            (seq {
                40
                1
            })

    Assert.Equal(1, lines)

[<Fact>]
let ``nothing left to show is still a step`` () =
    let lines = ViewerScroll.filling 10 Seq.empty

    Assert.Equal(1, lines)

[<Fact>]
let ``a page is measured without reading past it`` () =
    let mutable read = 0

    let heights =
        seq {
            for _ in 1..1000 do
                read <- read + 1
                yield 1
        }

    let lines = ViewerScroll.filling 20 heights

    Assert.Equal(20, lines)
    Assert.True(read < 100, $"{read} lines were measured to fill a page of 20")

[<Fact>]
let ``the start of a file is nought percent`` () =
    let percent = ViewerScroll.percent 0L 1000L

    Assert.Equal(0, percent)

[<Fact>]
let ``halfway through is half`` () =
    let percent = ViewerScroll.percent 500L 1000L

    Assert.Equal(50, percent)

[<Fact>]
let ``the end is all of it`` () =
    let percent = ViewerScroll.percent 1000L 1000L

    Assert.Equal(100, percent)

[<Fact>]
let ``an empty file is all shown at once`` () =
    let percent = ViewerScroll.percent 0L 0L

    Assert.Equal(100, percent)
