module Marbas.Ui.Viewer.ViewerHeader

open Marbas.Ui.Format

[<Literal>]
let Ellipsis = "…"

[<Literal>]
let private Gap = "  "

type Place =
    | AtLine of line: int
    | AtOffset of offset: int64

type Bar =
    { Name: string
      Mode: ViewerMode
      Encoding: string
      Length: int64
      Place: Place
      Percent: int }

let private modeText mode =
    match mode with
    | TextMode -> "Text"
    | HexMode -> "Hex"

let private placeText place =
    match place with
    | AtLine line -> $"line {line + 1}"
    | AtOffset offset -> offset.ToString "X8"

let private shorten width (text: string) =
    if text.Length <= width then text
    elif width <= Ellipsis.Length then text[text.Length - width ..]
    else Ellipsis + text[text.Length - width + Ellipsis.Length ..]

let private marks bar =
    [ modeText bar.Mode
      bar.Encoding
      $"{EntrySize.grouped bar.Length} B"
      placeText bar.Place
      $"{bar.Percent}%%" ]

let describe width bar =
    let right = marks bar |> String.concat Gap
    let room = width - right.Length - Gap.Length

    if room < 1 then
        shorten (max 0 width) right
    else
        (shorten room bar.Name).PadRight room + Gap + right
