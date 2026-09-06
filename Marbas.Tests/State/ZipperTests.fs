module Marbas.Tests.State.ZipperTests

open Xunit
open Marbas.State

let private three () =
    Zipper.single 1 |> Zipper.insert 2 |> Zipper.insert 3

[<Fact>]
let ``a single item is the current one and the only one`` () =
    let zipper = Zipper.single 7

    Assert.Equal(7, Zipper.current zipper)
    Assert.Equal(1, Zipper.count zipper)
    Assert.Equal(0, Zipper.position zipper)
    Assert.Equal<int list>([ 7 ], Zipper.items zipper)

[<Fact>]
let ``insert puts the item after the current one and makes it current`` () =
    let zipper = three () |> Zipper.at 0

    let inserted = zipper |> Zipper.insert 9

    Assert.Equal<int list>([ 1; 9; 2; 3 ], Zipper.items inserted)
    Assert.Equal(9, Zipper.current inserted)
    Assert.Equal(1, Zipper.position inserted)

[<Fact>]
let ``removing the current item moves to the one after it`` () =
    let zipper = three () |> Zipper.at 1

    let removed = zipper |> Zipper.remove

    Assert.Equal<int list>([ 1; 3 ], Zipper.items removed)
    Assert.Equal(3, Zipper.current removed)
    Assert.Equal(1, Zipper.position removed)

[<Fact>]
let ``removing the last item falls back to the one before it`` () =
    let zipper = three ()

    let removed = zipper |> Zipper.remove

    Assert.Equal<int list>([ 1; 2 ], Zipper.items removed)
    Assert.Equal(2, Zipper.current removed)
    Assert.Equal(1, Zipper.position removed)

[<Fact>]
let ``the only item cannot be removed`` () =
    let zipper = Zipper.single 5

    let removed = zipper |> Zipper.remove

    Assert.Equal<int list>([ 5 ], Zipper.items removed)
    Assert.Equal(5, Zipper.current removed)

[<Theory>]
[<InlineData(-4, 1)>]
[<InlineData(0, 1)>]
[<InlineData(1, 2)>]
[<InlineData(2, 3)>]
[<InlineData(9, 3)>]
let ``moving to a position clamps to the ends`` (position: int) (expected: int) =
    let zipper = three ()

    let moved = zipper |> Zipper.at position

    Assert.Equal(expected, Zipper.current moved)
    Assert.Equal<int list>([ 1; 2; 3 ], Zipper.items moved)

[<Theory>]
[<InlineData(0, 2)>]
[<InlineData(1, 3)>]
[<InlineData(2, 1)>]
let ``forward moves on and wraps round to the first`` (position: int) (expected: int) =
    let zipper = three () |> Zipper.at position

    let moved = zipper |> Zipper.forward

    Assert.Equal(expected, Zipper.current moved)

[<Theory>]
[<InlineData(0, 3)>]
[<InlineData(1, 1)>]
[<InlineData(2, 2)>]
let ``backward moves back and wraps round to the last`` (position: int) (expected: int) =
    let zipper = three () |> Zipper.at position

    let moved = zipper |> Zipper.backward

    Assert.Equal(expected, Zipper.current moved)

[<Fact>]
let ``update replaces the current item and leaves the rest`` () =
    let zipper = three () |> Zipper.at 1

    let updated = zipper |> Zipper.update (fun item -> item * 10)

    Assert.Equal<int list>([ 1; 20; 3 ], Zipper.items updated)
    Assert.Equal(1, Zipper.position updated)
