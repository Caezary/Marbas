module Marbas.Tests.Ui.Viewer.CodePageTests

open Xunit
open Marbas.FileSystem
open Marbas.Ui.Viewer

let private utf8 = { Charset = Utf8; Bom = WithoutBom }

let private marked = [| 0xEFuy; 0xBBuy; 0xBFuy; 0x41uy |]

[<Fact>]
let ``auto comes first and names what it settled on, then every code page`` () =
    let labels = CodePage.offered |> List.map (CodePage.label utf8)

    Assert.Equal<string list>(
        [ "Auto (UTF-8)"
          "UTF-8"
          "UTF-16 LE"
          "UTF-16 BE"
          "Latin-1"
          "Windows-1250"
          "CP852"
          "ISO-8859-2"
          "Windows-1252"
          "CP437" ],
        labels
    )

[<Fact>]
let ``auto reads the file the way the viewer settled it`` () =
    let detected = { Charset = Utf16Le; Bom = WithBom }

    let encoding = CodePage.encoding detected marked Automatic

    Assert.Equal(detected, encoding)

[<Fact>]
let ``a fixed code page reads the file in its own charset`` () =
    let encoding = CodePage.encoding utf8 marked (Fixed Cp852)

    Assert.Equal({ Charset = Cp852; Bom = WithoutBom }, encoding)

[<Fact>]
let ``a fixed code page steps over the mark the file starts with`` () =
    let encoding = CodePage.encoding utf8 marked (Fixed Utf8)

    Assert.Equal({ Charset = Utf8; Bom = WithBom }, encoding)
