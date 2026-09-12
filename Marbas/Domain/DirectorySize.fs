namespace Marbas.Domain

type DirectorySize =
    | Exact of bytes: int64
    | AtLeast of bytes: int64

module DirectorySize =

    let bytes size =
        match size with
        | Exact counted
        | AtLeast counted -> counted

    let nothingCounted = Exact 0L

    let add left right =
        match left, right with
        | Exact counted, Exact more -> Exact(counted + more)
        | _ -> AtLeast(bytes left + bytes right)

    let total sizes = sizes |> List.fold add nothingCounted
