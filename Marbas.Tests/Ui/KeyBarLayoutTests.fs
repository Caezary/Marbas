module Marbas.Tests.Ui.KeyBarLayoutTests

open Xunit
open Marbas.Ui

[<Fact>]
let ``the cells tile the whole width, leaving no column out`` () =
    let cells = KeyBarLayout.cells 12 80

    Assert.Equal(0, cells.Head.X)
    Assert.Equal(80, cells |> List.sumBy _.Width)

    for previous, next in List.pairwise cells do
        Assert.Equal(previous.X + previous.Width, next.X)

[<Fact>]
let ``a width that divides evenly gives cells of one size`` () =
    let cells = KeyBarLayout.cells 12 120

    Assert.Equal<int list>(List.replicate 12 10, cells |> List.map _.Width)

[<Fact>]
let ``the remainder is spread along the row, not heaped on one end`` () =
    let widths = KeyBarLayout.cells 12 80 |> List.map _.Width

    Assert.Equal<int list>([ 6; 7; 7; 6; 7; 7; 6; 7; 7; 6; 7; 7 ], widths)

[<Fact>]
let ``no two cells differ by more than a column`` () =
    for width in 1..200 do
        let widths = KeyBarLayout.cells 12 width |> List.map _.Width

        Assert.True(List.max widths - List.min widths <= 1, string width)

[<Fact>]
let ``a bar with no width has cells with no width`` () =
    let cells = KeyBarLayout.cells 12 0

    Assert.Equal<int list>(List.replicate 12 0, cells |> List.map _.Width)

[<Fact>]
let ``a cell is the one the whole row puts at that index`` () =
    let cells = KeyBarLayout.cells 12 97

    for index in 0..11 do
        Assert.Equal(cells[index], KeyBarLayout.cell 12 index 97)
