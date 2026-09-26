module Marbas.Tests.FileSystem.TextEncodingTests

open System.Text
open Xunit
open Marbas.FileSystem

let private bytes (text: string) = Encoding.ASCII.GetBytes text

let private wide (text: string) = Encoding.Unicode.GetBytes text

let private wideBigEndian (text: string) = Encoding.BigEndianUnicode.GetBytes text

[<Fact>]
let ``plain ascii is read as utf-8`` () =
    let probe = bytes "{ \"name\": \"value\" }"

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf8; Bom = WithoutBom }, encoding)

[<Fact>]
let ``a utf-8 byte order mark is recognised`` () =
    let probe = Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] (bytes "first")

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf8; Bom = WithBom }, encoding)

[<Fact>]
let ``multi-byte utf-8 without a mark is still utf-8`` () =
    let probe = Encoding.UTF8.GetBytes "zażółć gęślą jaźń"

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf8; Bom = WithoutBom }, encoding)

[<Fact>]
let ``a sequence cut off by the end of the probe does not disqualify utf-8`` () =
    let whole = Encoding.UTF8.GetBytes "ółć"
    let probe = whole[.. whole.Length - 2]

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf8; Bom = WithoutBom }, encoding)

[<Fact>]
let ``bytes that no utf-8 sequence allows fall back to latin-1`` () =
    let probe = [| 0x41uy; 0xFFuy; 0xFEuy; 0x42uy; 0xC0uy; 0x80uy |]

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Latin1; Bom = WithoutBom }, encoding)

[<Fact>]
let ``a little endian utf-16 mark is recognised`` () =
    let probe = Array.append [| 0xFFuy; 0xFEuy |] (wide "first")

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf16Le; Bom = WithBom }, encoding)

[<Fact>]
let ``a big endian utf-16 mark is recognised`` () =
    let probe = Array.append [| 0xFEuy; 0xFFuy |] (wideBigEndian "first")

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf16Be; Bom = WithBom }, encoding)

[<Fact>]
let ``little endian utf-16 without a mark is recognised by its zero bytes`` () =
    let probe = wide "{ \"name\": \"value\" }"

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf16Le; Bom = WithoutBom }, encoding)

[<Fact>]
let ``big endian utf-16 without a mark is recognised by its zero bytes`` () =
    let probe = wideBigEndian "{ \"name\": \"value\" }"

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf16Be; Bom = WithoutBom }, encoding)

[<Fact>]
let ``zero bytes on both sides of a pair are no text at all`` () =
    let probe = Array.zeroCreate<byte> 64

    let encoding = TextEncoding.detect probe

    Assert.Equal(Latin1, encoding.Charset)

[<Fact>]
let ``an empty probe is read as utf-8`` () =
    let probe = Array.empty<byte>

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf8; Bom = WithoutBom }, encoding)

[<Fact>]
let ``every charset says its name`` () =
    let names =
        [ Utf8; Utf16Le; Utf16Be; Latin1 ]
        |> List.map (fun charset -> TextEncoding.name { Charset = charset; Bom = WithoutBom })

    Assert.Equal<string list>([ "UTF-8"; "UTF-16 LE"; "UTF-16 BE"; "Latin-1" ], names)

[<Fact>]
let ``a mark is not part of the content`` () =
    let marked = { Charset = Utf8; Bom = WithBom }

    let length = TextEncoding.preambleLength marked

    Assert.Equal(3, length)

[<Fact>]
let ``a utf-16 mark is two bytes long`` () =
    let marked = { Charset = Utf16Le; Bom = WithBom }

    let length = TextEncoding.preambleLength marked

    Assert.Equal(2, length)

[<Fact>]
let ``without a mark the content starts at the first byte`` () =
    let plain = { Charset = Utf16Le; Bom = WithoutBom }

    let length = TextEncoding.preambleLength plain

    Assert.Equal(0, length)

[<Fact>]
let ``a wide charset counts two bytes to the character`` () =
    let sizes =
        [ Utf8; Utf16Le; Utf16Be; Latin1 ]
        |> List.map (fun charset -> TextEncoding.unitSize { Charset = charset; Bom = WithoutBom })

    Assert.Equal<int list>([ 1; 2; 2; 1 ], sizes)

[<Fact>]
let ``a line feed is one byte in utf-8 and two in utf-16`` () =
    let feeds =
        [ Utf8; Utf16Le; Utf16Be ]
        |> List.map (fun charset -> TextEncoding.lineFeed { Charset = charset; Bom = WithoutBom })

    Assert.Equal<byte array list>([ [| 0x0Auy |]; [| 0x0Auy; 0x00uy |]; [| 0x00uy; 0x0Auy |] ], feeds)

[<Fact>]
let ``utf-16 bytes decode to the text they carry`` () =
    let encoding = { Charset = Utf16Le; Bom = WithoutBom }

    let text = TextEncoding.decode encoding (wide "first")

    Assert.Equal("first", text)

[<Fact>]
let ``latin-1 bytes decode one character to the byte`` () =
    let encoding = { Charset = Latin1; Bom = WithoutBom }

    let text = TextEncoding.decode encoding [| 0x41uy; 0xE9uy |]

    Assert.Equal("Aé", text)

[<Fact>]
let ``a nul byte with no mark and no wide pattern means binary`` () =
    let probe = Array.append (bytes "MZ") [| 0x00uy; 0x01uy; 0x02uy |]

    let content = TextEncoding.contentOf probe

    Assert.Equal<Content>(Binary, content)

[<Fact>]
let ``plain text is not binary`` () =
    let probe = bytes "{ \"name\": \"value\" }"

    let content = TextEncoding.contentOf probe

    Assert.Equal<Content>(Textual, content)

[<Fact>]
let ``marked utf-16 is text, nul bytes and all`` () =
    let probe = Array.append [| 0xFFuy; 0xFEuy |] (wide "first")

    let content = TextEncoding.contentOf probe

    Assert.Equal<Content>(Textual, content)

[<Fact>]
let ``unmarked utf-16 is text, nul bytes and all`` () =
    let probe = wide "first line of the file"

    let content = TextEncoding.contentOf probe

    Assert.Equal<Content>(Textual, content)
