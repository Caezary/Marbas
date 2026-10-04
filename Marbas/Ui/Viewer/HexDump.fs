module Marbas.Ui.Viewer.HexDump

open System

[<Literal>]
let BytesPerLine = 16

let private bytesPerHalf = BytesPerLine / 2

let private halfWidth = bytesPerHalf * 3 - 1

[<Literal>]
let private Rule = " │ "

let private printable (byte: byte) =
    if byte >= 32uy && byte <= 126uy then char byte else '.'

let private hexHalf (bytes: byte array) =
    let hex = bytes |> Array.map (fun b -> b.ToString "X2") |> String.concat " "
    hex.PadRight halfWidth

let rowCount (length: int64) =
    int ((length + int64 BytesPerLine - 1L) / int64 BytesPerLine)

let offsetOf (row: int) = int64 row * int64 BytesPerLine

let offsetText (offset: int64) = offset.ToString "X10"

let row (offset: int64) (bytes: byte array) =
    let first, second = bytes |> Array.splitAt (min bytesPerHalf bytes.Length)
    let text = bytes |> Array.map printable |> String

    $"""{offsetText offset}  {hexHalf first}{Rule}{hexHalf second}  {text}"""

let rows (offset: int64) (bytes: byte array) =
    bytes
    |> Array.chunkBySize BytesPerLine
    |> Array.mapi (fun index -> row (offset + offsetOf index))
    |> List.ofArray
