module Marbas.Tests.Ui.PageJumpTests

open Xunit
open Marbas.Ui.PageJump

let private page first height count =
    { FirstVisible = first
      Height = height
      Count = count }

[<Fact>]
let ``the first jump forward lands on the last row of the page`` () =
    let target = forward (page 0 10 100) 3

    Assert.Equal(9, target)

[<Fact>]
let ``a second jump forward moves on by a whole page`` () =
    let target = forward (page 0 10 100) 9

    Assert.Equal(19, target)

[<Fact>]
let ``the last page of a listing is where jumping forward stops`` () =
    let target = forward (page 90 10 95) 94

    Assert.Equal(94, target)

[<Fact>]
let ``the first jump back lands on the first row of the page`` () =
    let target = backward (page 20 10 100) 25

    Assert.Equal(20, target)

[<Fact>]
let ``a second jump back moves on by a whole page`` () =
    let target = backward (page 20 10 100) 20

    Assert.Equal(10, target)

[<Fact>]
let ``the top of a listing is where jumping back stops`` () =
    let target = backward (page 0 10 100) 0

    Assert.Equal(0, target)

[<Fact>]
let ``a page shorter than the listing still moves one row at a time`` () =
    let target = forward (page 0 0 100) 0

    Assert.Equal(1, target)

[<Fact>]
let ``an empty listing has nowhere to jump`` () =
    let targets = [ forward (page 0 10 0) 0; backward (page 0 10 0) 0 ]

    Assert.Equal<int list>([ 0; 0 ], targets)
