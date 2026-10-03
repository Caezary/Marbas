module Marbas.Tests.Domain.FavoriteSlotTests

open Xunit
open Marbas.Domain

[<Theory>]
[<InlineData(0)>]
[<InlineData(5)>]
[<InlineData(9)>]
let ``a digit is a slot of its own`` (digit: int) =
    let slot = FavoriteSlot.tryCreate digit

    Assert.Equal(Some digit, slot |> Option.map FavoriteSlot.value)

[<Theory>]
[<InlineData(-1)>]
[<InlineData(10)>]
let ``a number that is not one digit is no slot`` (number: int) =
    let slot = FavoriteSlot.tryCreate number

    Assert.Equal(None, slot)

[<Fact>]
let ``there are ten slots, from 0 to 9`` () =
    let digits = FavoriteSlot.all |> List.map FavoriteSlot.value

    Assert.Equal<int list>([ 0..9 ], digits)
