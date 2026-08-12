module Marbas.Tests.Ui.HexDumpTests

open Xunit
open Marbas.Ui

[<Fact>]
let ``a full row shows the offset, every byte and its text`` () =
    let bytes = [| 0uy..15uy |]

    let lines = HexDump.describe bytes

    Assert.Equal(
        "00000000  00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F  ................",
        List.exactlyOne lines
    )

[<Fact>]
let ``printable bytes are shown as themselves`` () =
    let bytes = "Hi!"B

    let lines = HexDump.describe bytes

    Assert.EndsWith("Hi!", List.exactlyOne lines)
    Assert.Contains("48 69 21", List.exactlyOne lines)

[<Fact>]
let ``rows are numbered by the offset they start at`` () =
    let bytes = Array.zeroCreate 33

    let lines = HexDump.describe bytes

    Assert.Equal<string list>(
        [ "00000000"; "00000010"; "00000020" ],
        lines |> List.map (fun line -> line.Substring(0, 8))
    )

[<Fact>]
let ``a short last row is padded so the text column keeps its place`` () =
    let bytes = Array.zeroCreate 20

    let lines = HexDump.describe bytes
    let textStart (line: string) = line.LastIndexOf "  "

    Assert.Equal(textStart lines.Head, textStart (List.last lines))

[<Fact>]
let ``no bytes make no rows`` () =
    let lines = HexDump.describe [||]

    Assert.Empty lines
