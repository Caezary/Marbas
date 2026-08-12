module Marbas.Ui.HexDump

open System

[<Literal>]
let BytesPerLine = 16

let private hexColumnWidth = BytesPerLine * 3 - 1

let private printable (byte: byte) =
    if byte >= 32uy && byte <= 126uy then char byte else '.'

let private describeRow (offset: int) (bytes: byte array) =
    let hex = bytes |> Array.map (fun b -> b.ToString "X2") |> String.concat " "
    let text = bytes |> Array.map printable |> String

    $"""{offset.ToString "X8"}  {hex.PadRight hexColumnWidth}  {text}"""

let describe (bytes: byte array) =
    bytes
    |> Array.chunkBySize BytesPerLine
    |> Array.mapi (fun index row -> describeRow (index * BytesPerLine) row)
    |> List.ofArray
