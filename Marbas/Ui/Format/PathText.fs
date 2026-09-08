module Marbas.Ui.Format.PathText

[<Literal>]
let Ellipsis = "..."

let private joined (separator: char) (parts: string list) = String.concat (string separator) parts

let private ending width (text: string) =
    text.Substring(text.Length - (width |> max 0 |> min text.Length))

let private shortenings (separator: char) (parts: string list) =
    let root = List.head parts

    match List.rev parts with
    | current :: above :: _ :: _ ->
        [ joined separator [ root; Ellipsis; above; current ]
          joined separator [ root; Ellipsis; current ]
          joined separator [ Ellipsis; current ] ]
    | current :: _ :: _ -> [ joined separator [ Ellipsis; current ] ]
    | _ -> []

let shorten (separator: char) (width: int) (text: string) =
    let fitting () =
        text.Split separator
        |> List.ofArray
        |> shortenings separator 
        |> List.tryFind (fun candidate -> candidate.Length <= width)
        |> Option.defaultValue (ending width text)

    if width <= 0 then ""
    elif text.Length <= width then text
    else fitting ()
