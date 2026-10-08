namespace Marbas.Domain

type SortKey =
    | ByName
    | ByExtension
    | ByDate
    | BySize

type SortDirection =
    | Ascending
    | Descending

type SortOrder =
    { Key: SortKey
      Direction: SortDirection }

module SortOrder =

    let create key direction = { Key = key; Direction = direction }

    let byName = create ByName Ascending

    let private reverse direction =
        match direction with
        | Ascending -> Descending
        | Descending -> Ascending

    let switchTo key order =
        if order.Key = key then
            { order with
                Direction = reverse order.Direction }
        else
            create key Ascending
