module Marbas.Ui.Viewer.HexDump

open Marbas.FileSystem

[<Literal>]
let BytesPerLine = 16

let private bytesPerHalf = BytesPerLine / 2

let private halfWidth = bytesPerHalf * 3 - 1

[<Literal>]
let private Rule = " │ "

[<Literal>]
let private Gap = "  "

[<Literal>]
let private OffsetDigits = 10

let private hexHalf (bytes: byte array) =
    let hex = bytes |> Array.map (fun b -> b.ToString "X2") |> String.concat " "
    hex.PadRight halfWidth

let rowCount (length: int64) =
    int ((length + int64 BytesPerLine - 1L) / int64 BytesPerLine)

let offsetOf (row: int) = int64 row * int64 BytesPerLine

let offsetText (offset: int64) = offset.ToString $"X{OffsetDigits}"

let private hexStart = OffsetDigits + Gap.Length

let hexColumn index =
    let pastRule = if index >= bytesPerHalf then Rule.Length - 1 else 0
    hexStart + index * 3 + pastRule

let textColumn index =
    hexStart + halfWidth * 2 + Rule.Length + Gap.Length + index

let row encoding (offset: int64) (bytes: RowBytes) =
    let first, second =
        bytes.Bytes |> Array.splitAt (min bytesPerHalf bytes.Bytes.Length)

    let text = HexText.cells encoding bytes

    $"""{offsetText offset}{Gap}{hexHalf first}{Rule}{hexHalf second}{Gap}{text}"""

let rows encoding (offset: int64) (bytes: byte array) =
    [ for index in 0 .. rowCount bytes.LongLength - 1 ->
          RowBytes.within bytes (index * BytesPerLine) BytesPerLine
          |> row encoding (offset + offsetOf index) ]

let bytesAt (source: FileWindow.Source) (row: int) =
    let offset = offsetOf row

    if offset >= source.Length then
        None
    else
        let before = int (min offset (int64 RowBytes.Context))

        let window =
            source.Read(offset - int64 before, before + BytesPerLine + RowBytes.Context)

        Some(RowBytes.within window before BytesPerLine)
