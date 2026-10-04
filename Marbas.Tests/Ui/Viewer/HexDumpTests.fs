module Marbas.Tests.Ui.Viewer.HexDumpTests

open Xunit
open Marbas.Ui.Viewer

[<Fact>]
let ``a full row shows the offset, every byte and its text`` () =
    let bytes = [| 0uy..15uy |]

    let line = HexDump.row 0L bytes

    Assert.Equal("0000000000  00 01 02 03 04 05 06 07 │ 08 09 0A 0B 0C 0D 0E 0F  ................", line)

[<Fact>]
let ``a full row fits eighty columns`` () =
    let bytes = [| 0uy..15uy |]

    let line = HexDump.row 0L bytes

    Assert.Equal(79, line.Length)

[<Fact>]
let ``a short row past the eighth byte keeps the rule between the halves`` () =
    let bytes = [| 0uy..11uy |]

    let line = HexDump.row 0L bytes

    Assert.Equal("0000000000  00 01 02 03 04 05 06 07 │ 08 09 0A 0B              ............", line)

[<Fact>]
let ``a short row within the first half keeps the rule in its column`` () =
    let bytes = "Hi!"B

    let line = HexDump.row 0L bytes

    Assert.Equal("0000000000  48 69 21                │                          Hi!", line)

[<Fact>]
let ``printable bytes are shown as themselves`` () =
    let bytes = "Hi!"B

    let line = HexDump.row 0L bytes

    Assert.EndsWith("Hi!", line)
    Assert.Contains("48 69 21", line)

[<Fact>]
let ``a row is numbered by the offset it starts at`` () =
    let line = HexDump.row 4096L [| 0uy |]

    Assert.StartsWith("0000001000  ", line)

[<Fact>]
let ``an offset past four gigabytes keeps every digit`` () =
    let line = HexDump.row 0xFFFFFFFFF0L [| 0uy |]

    Assert.StartsWith("FFFFFFFFF0  ", line)

[<Fact>]
let ``rows are numbered on from the offset they were read from`` () =
    let bytes = Array.zeroCreate 33

    let lines = HexDump.rows 16L bytes

    Assert.Equal<string list>(
        [ "0000000010"; "0000000020"; "0000000030" ],
        lines |> List.map _.Substring(0, 10)
    )

[<Fact>]
let ``a short last row is padded so the text column keeps its place`` () =
    let bytes = Array.zeroCreate 20

    let lines = HexDump.rows 0L bytes
    let textStart (line: string) = line.LastIndexOf "  "

    Assert.Equal(textStart lines.Head, textStart (List.last lines))

[<Fact>]
let ``no bytes make no rows`` () =
    let lines = HexDump.rows 0L [||]

    Assert.Empty lines

[<Fact>]
let ``a file takes a row for every sixteen bytes it holds`` () =
    let counts = [ 0L; 1L; 16L; 17L; 4096L ] |> List.map HexDump.rowCount

    Assert.Equal<int list>([ 0; 1; 1; 2; 256 ], counts)

[<Fact>]
let ``a row starts sixteen bytes on from the one before it`` () =
    let offsets = [ 0; 1; 2 ] |> List.map HexDump.offsetOf

    Assert.Equal<int64 list>([ 0L; 16L; 32L ], offsets)
