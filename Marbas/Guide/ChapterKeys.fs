module Marbas.Guide.ChapterKeys

let private runMark = "…"

let private keyTableHeader =
    Row(
        [ "Key"; "Alternative"; "Action" ]
        |> List.map (fun title -> Cell(Phrase [ Text title ]))
    )

let private stemOf (name: string) = name.Substring(0, name.Length - 1)

let private runOf (first: string) (last: string) =
    if first.Length = last.Length && stemOf first = stemOf last then
        [ first[first.Length - 1] .. last[last.Length - 1] ]
        |> List.map (fun character -> stemOf first + string character)
    else
        [ first; last ]

let rec private namesIn spans =
    match spans with
    | Code first :: Text between :: Code last :: rest when between.Trim() = runMark -> runOf first last @ namesIn rest
    | Code name :: rest -> name :: namesIn rest
    | _ :: rest -> namesIn rest
    | [] -> []

let private keyCells (Row cells) =
    cells
    |> List.truncate 2
    |> List.collect (fun (Cell(Phrase spans)) -> namesIn spans)

let private keysOf block =
    match block with
    | Table table when Table.header table = keyTableHeader -> Table.body table |> List.collect keyCells
    | _ -> []

let named (chapters: Chapter list) =
    chapters
    |> List.collect (fun chapter -> List.collect keysOf chapter.Contents)
    |> List.map KeyName.create
    |> Set.ofList
