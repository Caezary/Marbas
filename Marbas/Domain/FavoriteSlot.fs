namespace Marbas.Domain

type FavoriteSlot = private FavoriteSlot of int

module FavoriteSlot =

    [<Literal>]
    let private Lowest = 0

    [<Literal>]
    let private Highest = 9

    let tryCreate digit =
        if digit >= Lowest && digit <= Highest then
            Some(FavoriteSlot digit)
        else
            None

    let value (FavoriteSlot digit) = digit

    let all = [ Lowest..Highest ] |> List.map FavoriteSlot
