module Marbas.Tests.Ui.Viewer.HexDumpTests

open System
open System.Text
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Viewing
open Marbas.Ui.Viewer
open Marbas.Tests

let private latin1 = { Charset = Latin1; Bom = WithoutBom }

let private utf8 = { Charset = Utf8; Bom = WithoutBom }

let private alone bytes =
    { Before = [||]
      Bytes = bytes
      After = [||] }

let private sourceOf (bytes: byte array) =
    let fs = MockFileSystem()
    let path = Root.at "start/data.bin"
    fs.AddFile(path, MockFileData bytes)
    FileWindow.openFile fs (FileSystemPath.create path) |> Expect.ok

let private splitCharacter =
    Array.concat [ Array.create 15 0x41uy; Encoding.UTF8.GetBytes "ż"; "B"B ]

[<Fact>]
let ``a full row shows the offset, every byte and its text`` () =
    let bytes = [| 0uy .. 15uy |]

    let line = HexDump.row latin1 0L (alone bytes)

    Assert.Equal("0000000000  00 01 02 03 04 05 06 07 │ 08 09 0A 0B 0C 0D 0E 0F  ................", line)

[<Fact>]
let ``a full row fits eighty columns`` () =
    let bytes = [| 0uy .. 15uy |]

    let line = HexDump.row latin1 0L (alone bytes)

    Assert.Equal(79, line.Length)

[<Fact>]
let ``a short row past the eighth byte keeps the rule between the halves`` () =
    let bytes = [| 0uy .. 11uy |]

    let line = HexDump.row latin1 0L (alone bytes)

    Assert.Equal("0000000000  00 01 02 03 04 05 06 07 │ 08 09 0A 0B              ............", line)

[<Fact>]
let ``a short row within the first half keeps the rule in its column`` () =
    let bytes = "Hi!"B

    let line = HexDump.row latin1 0L (alone bytes)

    Assert.Equal("0000000000  48 69 21                │                          Hi!", line)

[<Fact>]
let ``printable bytes are shown as themselves`` () =
    let bytes = "Hi!"B

    let line = HexDump.row latin1 0L (alone bytes)

    Assert.EndsWith("Hi!", line)
    Assert.Contains("48 69 21", line)

[<Fact>]
let ``a row is numbered by the offset it starts at`` () =
    let line = HexDump.row latin1 4096L (alone [| 0uy |])

    Assert.StartsWith("0000001000  ", line)

[<Fact>]
let ``an offset past four gigabytes keeps every digit`` () =
    let line = HexDump.row latin1 0xFFFFFFFFF0L (alone [| 0uy |])

    Assert.StartsWith("FFFFFFFFF0  ", line)

[<Fact>]
let ``rows are numbered on from the offset they were read from`` () =
    let bytes = Array.zeroCreate 33

    let lines = HexDump.rows latin1 16L bytes

    Assert.Equal<string list>([ "0000000010"; "0000000020"; "0000000030" ], lines |> List.map _.Substring(0, 10))

[<Fact>]
let ``a short last row is padded so the text column keeps its place`` () =
    let bytes = Array.zeroCreate 20

    let lines = HexDump.rows latin1 0L bytes

    let textStart (line: string) =
        line.LastIndexOf("  ", StringComparison.Ordinal)

    Assert.Equal(textStart lines.Head, textStart (List.last lines))

[<Fact>]
let ``no bytes make no rows`` () =
    let lines = HexDump.rows latin1 0L [||]

    Assert.Empty lines

[<Fact>]
let ``a file takes a row for every sixteen bytes it holds`` () =
    let counts = [ 0L; 1L; 16L; 17L; 4096L ] |> List.map HexDump.rowCount

    Assert.Equal<int list>([ 0; 1; 1; 2; 256 ], counts)

[<Fact>]
let ``a row starts sixteen bytes on from the one before it`` () =
    let offsets = [ 0; 1; 2 ] |> List.map HexDump.offsetOf

    Assert.Equal<int64 list>([ 0L; 16L; 32L ], offsets)

[<Fact>]
let ``the character column reads the bytes in the encoding it is given`` () =
    let bytes = [| 0x41uy; 0xA5uy |]

    let line = HexDump.row { Charset = Cp852; Bom = WithoutBom } 0L (alone bytes)

    Assert.EndsWith("Aą", line)

[<Fact>]
let ``a character split between two rows stands at the end of the first`` () =
    let lines = HexDump.rows utf8 0L splitCharacter

    Assert.EndsWith("AAAAAAAAAAAAAAAż", lines.Head)
    Assert.EndsWith("   B", List.last lines)

[<Fact>]
let ``a row read from the file carries the bytes on either side of it`` () =
    use source = sourceOf splitCharacter

    let row = HexDump.bytesAt source 1

    Assert.Equal(
        Some
            { Before = [| 0x41uy; 0x41uy; 0xC5uy |]
              Bytes = [| 0xBCuy; 0x42uy |]
              After = [||] },
        row
    )

[<Fact>]
let ``no row is read past the end of the file`` () =
    use source = sourceOf splitCharacter

    let row = HexDump.bytesAt source 2

    Assert.Equal(None, row)

[<Theory>]
[<InlineData(0)>]
[<InlineData(7)>]
[<InlineData(8)>]
[<InlineData(15)>]
let ``a byte's hex column holds its two digits`` (index: int) =
    let line = HexDump.row latin1 0L (alone [| 0xA0uy .. 0xAFuy |])

    let digits = line.Substring(HexDump.hexColumn index, 2)

    Assert.Equal((0xA0 + index).ToString "X2", digits)

[<Theory>]
[<InlineData(0)>]
[<InlineData(7)>]
[<InlineData(8)>]
[<InlineData(15)>]
let ``a byte's text column holds its character`` (index: int) =
    let line = HexDump.row latin1 0L (alone [| 0x41uy .. 0x50uy |])

    let shown = line[HexDump.textColumn index]

    Assert.Equal(char (0x41 + index), shown)
