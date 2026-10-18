module Marbas.Ui.Viewer.HexMark

open Marbas.Viewing

let private half = HexDump.BytesPerLine / 2

let private hexDigits = 2

let private hexSpan first last =
    HexDump.hexColumn first, HexDump.hexColumn last + hexDigits

let private hexSpans first last =
    if first < half && last >= half then
        [ hexSpan first (half - 1); hexSpan half last ]
    else
        [ hexSpan first last ]

let spans (hit: ByteSearch.Match) (row: int) =
    let rowStart = HexDump.offsetOf row
    let rowEnd = rowStart + int64 HexDump.BytesPerLine - 1L
    let first = int (max hit.Offset rowStart - rowStart)
    let last = int (min (hit.Offset + int64 hit.Length - 1L) rowEnd - rowStart)

    if first > last then
        []
    else
        hexSpans first last @ [ HexDump.textColumn first, HexDump.textColumn last + 1 ]
