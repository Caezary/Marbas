module Marbas.Tests.FileSystem.ArchiveNamesTests

open System.Globalization
open System.Text
open Xunit
open SharpCompress.Common
open Marbas.FileSystem
open Marbas.Tests

let private polish = "zażółć.txt"

let private legacy = ArchiveNames.legacyEncoding 852

let private decoded claimed (bytes: byte array) =
    ArchiveNames.decode legacy bytes 0 bytes.Length claimed

[<Fact>]
let ``a name the archive calls utf-8 is read as utf-8`` () =
    let bytes = Encoding.UTF8.GetBytes polish

    let name = decoded EncodingType.UTF8 bytes

    Assert.Equal(polish, name)

[<Fact>]
let ``a name the archive says nothing about is read as utf-8 when it is valid utf-8`` () =
    let bytes = Encoding.UTF8.GetBytes polish

    let name = decoded EncodingType.Default bytes

    Assert.Equal(polish, name)

[<Fact>]
let ``a name that is no utf-8 falls back to the legacy code page`` () =
    let bytes = legacy.GetBytes polish

    let name = decoded EncodingType.Default bytes

    Assert.Equal(polish, name)

[<Fact>]
let ``a plain name reads the same either way`` () =
    let bytes = Encoding.ASCII.GetBytes "notes.txt"

    let name = decoded EncodingType.Default bytes

    Assert.Equal("notes.txt", name)

[<Fact>]
let ``a sequence cut in half is no utf-8`` () =
    let whole = Encoding.UTF8.GetBytes "ół"
    let cut = whole[.. whole.Length - 2]

    let name = decoded EncodingType.Default cut

    Assert.Equal(legacy.GetString cut, name)

[<Fact>]
let ``only the named part of the buffer is read`` () =
    let bytes = Encoding.ASCII.GetBytes "xxnotes.txtyy"

    let name = ArchiveNames.decode legacy bytes 2 9 EncodingType.Default

    Assert.Equal("notes.txt", name)

[<Fact>]
let ``a code page no encoding answers to falls back to latin-1`` () =
    let encoding = ArchiveNames.legacyEncoding 999999

    Assert.Equal(Encoding.Latin1, encoding)

[<Theory>]
[<InlineData(0)>]
[<InlineData(65001)>]
let ``a code page that says nothing falls back to latin-1`` (codePage: int) =
    let encoding = ArchiveNames.legacyEncoding codePage

    Assert.Equal(Encoding.Latin1, encoding)

[<Fact>]
let ``the legacy code page of a culture is its own`` () =
    let codePage = ArchiveNames.legacyCodePage CultureInfo.InvariantCulture

    Assert.Equal(CultureInfo.InvariantCulture.TextInfo.OEMCodePage, codePage)

[<WindowsFact "The OEM code page of a culture is a Windows concept">]
let ``a Polish culture reads legacy names as code page 852`` () =
    let codePage = ArchiveNames.legacyCodePage (CultureInfo "pl-PL")

    Assert.Equal(852, codePage)
