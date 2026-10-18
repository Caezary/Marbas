module Marbas.Tests.Ui.Viewer.HexTextTests

open System.Text
open Xunit
open Marbas.Viewing
open Marbas.Ui.Viewer

let private reading charset = { Charset = charset; Bom = WithoutBom }

let private alone bytes =
    { Before = [||]
      Bytes = bytes
      After = [||] }

[<Fact>]
let ``printable ascii shows as itself`` () =
    let row = alone "Hi!"B

    let shown = HexText.cells (reading Latin1) row

    Assert.Equal("Hi!", shown)

[<Fact>]
let ``a control byte shows as a dot`` () =
    let row = alone [| 0x00uy; 0x41uy; 0x7Fuy; 0x85uy |]

    let shown = HexText.cells (reading Latin1) row

    Assert.Equal(".A..", shown)

[<Fact>]
let ``a byte above ascii shows as the code page has it`` () =
    let row = alone [| 0xA5uy; 0xB9uy |]

    let shown =
        [ Latin1; Windows1250; Cp852; Iso88592; Windows1252; Cp437 ]
        |> List.map (fun charset -> HexText.cells (reading charset) row)

    Assert.Equal<string list>([ "¥¹"; "Ąą"; "ą╣"; "Ľš"; "¥¹"; "Ñ╣" ], shown)

[<Fact>]
let ``a utf-8 character stands over its first byte and blanks the rest`` () =
    let row = alone (Encoding.UTF8.GetBytes "Aż€")

    let shown = HexText.cells (reading Utf8) row

    Assert.Equal("Aż €  ", shown)

[<Fact>]
let ``utf-16 shows a character over every second byte`` () =
    let row = alone (Encoding.Unicode.GetBytes "Hi")

    let shown = HexText.cells (reading Utf16Le) row

    Assert.Equal("H i ", shown)

[<Fact>]
let ``a big-endian unit is read in its own byte order`` () =
    let row = alone (Encoding.BigEndianUnicode.GetBytes "Hi")

    let shown = HexText.cells (reading Utf16Be) row

    Assert.Equal("H i ", shown)

[<Fact>]
let ``a character begun in the row before is blanked`` () =
    let row =
        { Before = [| 0x41uy; 0xC5uy |]
          Bytes = [| 0xBCuy; 0x42uy |]
          After = [||] }

    let shown = HexText.cells (reading Utf8) row

    Assert.Equal(" B", shown)

[<Fact>]
let ``a surrogate pair begun in the row before is blanked`` () =
    let pair = Encoding.Unicode.GetBytes "A😀"

    let row =
        { Before = pair[1..3]
          Bytes = Array.append pair[4..5] [| 0x42uy; 0x00uy |]
          After = [||] }

    let shown = HexText.cells (reading Utf16Le) row

    Assert.Equal("  B ", shown)

[<Fact>]
let ``a character running into the next row shows where it starts`` () =
    let row =
        { Before = [||]
          Bytes = [| 0x41uy; 0xC5uy |]
          After = [| 0xBCuy |] }

    let shown = HexText.cells (reading Utf8) row

    Assert.Equal("Aż", shown)

[<Fact>]
let ``a utf-8 byte that starts nothing shows as a dot`` () =
    let row = alone [| 0xBCuy; 0x41uy |]

    let shown = HexText.cells (reading Utf8) row

    Assert.Equal(".A", shown)

[<Fact>]
let ``a character cut off by the end of the file shows as a dot`` () =
    let row = alone [| 0x41uy; 0xC5uy |]

    let shown = HexText.cells (reading Utf8) row

    Assert.Equal("A.", shown)

[<Fact>]
let ``a wide character takes two of its cells`` () =
    let row = alone (Encoding.UTF8.GetBytes "中")

    let shown = HexText.cells (reading Utf8) row

    Assert.Equal("中 ", shown)

[<Fact>]
let ``a wide character with one cell left in the row shows as a dot`` () =
    let wide = Encoding.UTF8.GetBytes "中"

    let row =
        { Before = [||]
          Bytes = [| 0x41uy; wide[0] |]
          After = wide[1..] }

    let shown = HexText.cells (reading Utf8) row

    Assert.Equal("A.", shown)

[<Fact>]
let ``a mark at the start of the file shows as a dot`` () =
    let row = alone (Encoding.UTF8.GetPreamble())

    let shown = HexText.cells (reading Utf8) row

    Assert.Equal(".  ", shown)
