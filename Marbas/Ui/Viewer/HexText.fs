module Marbas.Ui.Viewer.HexText

open System
open System.Text
open Terminal.Gui.Text
open Marbas.Viewing

[<Literal>]
let Placeholder = '.'

[<Literal>]
let private Filler = ' '

let private shownRune encoding (bytes: byte array) =
    match TextEncoding.decode encoding bytes |> _.EnumerateRunes() |> List.ofSeq with
    | [ rune ] when not (Rune.IsControl rune) && rune <> Rune.ReplacementChar -> Some rune
    | _ -> None

let private blank room = String(Filler, max 0 room)

let private placeholder room = string Placeholder + blank (room - 1)

let private character encoding (window: byte array) position length room =
    let fits (rune: Rune) =
        rune.GetColumns() >= 1 && rune.GetColumns() <= room

    match shownRune encoding (Array.sub window position length) |> Option.filter fits with
    | Some rune -> rune.ToString() + blank (room - rune.GetColumns())
    | None -> placeholder room

let cells encoding (row: RowBytes) =
    let window = Array.concat [ row.Before; row.Bytes; row.After ]
    let unit = TextEncoding.unitSize encoding
    let start = row.Before.Length
    let stop = start + row.Bytes.Length

    let step position =
        let length = TextEncoding.characterLengthAt encoding window position
        let next = position + Option.defaultValue unit length
        let reach = min next stop

        let shown =
            match length with
            | _ when position < start -> blank (reach - start)
            | Some length -> character encoding window position length (reach - position)
            | None -> placeholder (reach - position)

        shown, next

    let rec walk position (shown: string list) =
        if position >= stop then
            shown |> List.rev |> String.concat ""
        else
            let cells, next = step position
            walk next (cells :: shown)

    let firstAligned = start % unit
    walk firstAligned []
