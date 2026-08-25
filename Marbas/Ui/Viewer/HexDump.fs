module Marbas.Ui.Viewer.HexDump

open System

[<Literal>]
let BytesPerLine = 16

let private hexColumnWidth = BytesPerLine * 3 - 1

let private printable (byte: byte) =
    if byte >= 32uy && byte <= 126uy then char byte else '.'

let rowCount (length: int64) =
    int ((length + int64 BytesPerLine - 1L) / int64 BytesPerLine)

let offsetOf (row: int) = int64 row * int64 BytesPerLine

let row (offset: int64) (bytes: byte array) =
    let hex = bytes |> Array.map (fun b -> b.ToString "X2") |> String.concat " "
    let text = bytes |> Array.map printable |> String

    $"""{offset.ToString "X8"}  {hex.PadRight hexColumnWidth}  {text}"""

let rows (offset: int64) (bytes: byte array) =
    bytes
    |> Array.chunkBySize BytesPerLine
    |> Array.mapi (fun index -> row (offset + offsetOf index))
    |> List.ofArray
