module Marbas.Tests.Domain.SortOrderTests

open Xunit
open Marbas.Domain

[<Fact>]
let ``a panel starts sorted by name ascending`` () =
    let order = SortOrder.byName

    Assert.Equal(SortKey.ByName, order.Key)
    Assert.Equal(SortDirection.Ascending, order.Direction)

[<Fact>]
let ``switching to another key sorts ascending`` () =
    let order = SortOrder.create SortKey.ByName SortDirection.Descending

    let switched = SortOrder.switchTo SortKey.BySize order

    Assert.Equal(SortOrder.create SortKey.BySize SortDirection.Ascending, switched)

[<Fact>]
let ``switching to the key already in use reverses the direction`` () =
    let order = SortOrder.create SortKey.ByDate SortDirection.Ascending

    let switched = SortOrder.switchTo SortKey.ByDate order

    Assert.Equal(SortOrder.create SortKey.ByDate SortDirection.Descending, switched)

[<Fact>]
let ``switching to the same key twice comes back to ascending`` () =
    let order = SortOrder.create SortKey.ByExtension SortDirection.Ascending

    let switched = order
                |> SortOrder.switchTo SortKey.ByExtension
                |> SortOrder.switchTo SortKey.ByExtension

    Assert.Equal(order, switched)
