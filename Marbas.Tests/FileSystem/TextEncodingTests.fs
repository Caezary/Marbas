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

    Assert.Equal({ Charset = Utf8; HasBom = false }, encoding)

[<Fact>]
let ``a utf-8 byte order mark is recognised`` () =
    let probe = Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] (bytes "first")

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf8; HasBom = true }, encoding)

[<Fact>]
let ``multi-byte utf-8 without a mark is still utf-8`` () =
    let probe = Encoding.UTF8.GetBytes "zażółć gęślą jaźń"

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf8; HasBom = false }, encoding)

[<Fact>]
let ``a sequence cut off by the end of the probe does not disqualify utf-8`` () =
    let whole = Encoding.UTF8.GetBytes "ółć"
    let probe = whole[.. whole.Length - 2]

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf8; HasBom = false }, encoding)

[<Fact>]
let ``bytes that no utf-8 sequence allows fall back to latin-1`` () =
    let probe = [| 0x41uy; 0xFFuy; 0xFEuy; 0x42uy; 0xC0uy; 0x80uy |]

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Latin1; HasBom = false }, encoding)

[<Fact>]
let ``a little endian utf-16 mark is recognised`` () =
    let probe = Array.append [| 0xFFuy; 0xFEuy |] (wide "first")

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf16Le; HasBom = true }, encoding)

[<Fact>]
let ``a big endian utf-16 mark is recognised`` () =
    let probe = Array.append [| 0xFEuy; 0xFFuy |] (wideBigEndian "first")

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf16Be; HasBom = true }, encoding)

[<Fact>]
let ``little endian utf-16 without a mark is recognised by its zero bytes`` () =
    let probe = wide "{ \"name\": \"value\" }"

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf16Le; HasBom = false }, encoding)

[<Fact>]
let ``big endian utf-16 without a mark is recognised by its zero bytes`` () =
    let probe = wideBigEndian "{ \"name\": \"value\" }"

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf16Be; HasBom = false }, encoding)

[<Fact>]
let ``zero bytes on both sides of a pair are no text at all`` () =
    let probe = Array.zeroCreate<byte> 64

    let encoding = TextEncoding.detect probe

    Assert.Equal(Latin1, encoding.Charset)

[<Fact>]
let ``an empty probe is read as utf-8`` () =
    let probe = Array.empty<byte>

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf8; HasBom = false }, encoding)

[<Fact>]
let ``every charset says its name`` () =
    let names =
        [ Utf8; Utf16Le; Utf16Be; Latin1 ]
        |> List.map (fun charset -> TextEncoding.name { Charset = charset; HasBom = false })

    Assert.Equal<string list>([ "UTF-8"; "UTF-16 LE"; "UTF-16 BE"; "Latin-1" ], names)

[<Fact>]
let ``a mark is not part of the content`` () =
    let marked = { Charset = Utf8; HasBom = true }

    let length = TextEncoding.preambleLength marked

    Assert.Equal(3, length)

[<Fact>]
let ``a utf-16 mark is two bytes long`` () =
    let marked = { Charset = Utf16Le; HasBom = true }

    let length = TextEncoding.preambleLength marked

    Assert.Equal(2, length)

[<Fact>]
let ``without a mark the content starts at the first byte`` () =
    let plain = { Charset = Utf16Le; HasBom = false }

    let length = TextEncoding.preambleLength plain

    Assert.Equal(0, length)

[<Fact>]
let ``a wide charset counts two bytes to the character`` () =
    let sizes =
        [ Utf8; Utf16Le; Utf16Be; Latin1 ]
        |> List.map (fun charset -> TextEncoding.unitSize { Charset = charset; HasBom = false })

    Assert.Equal<int list>([ 1; 2; 2; 1 ], sizes)

[<Fact>]
let ``a line feed is one byte in utf-8 and two in utf-16`` () =
    let feeds =
        [ Utf8; Utf16Le; Utf16Be ]
        |> List.map (fun charset -> TextEncoding.lineFeed { Charset = charset; HasBom = false })

    Assert.Equal<byte array list>([ [| 0x0Auy |]; [| 0x0Auy; 0x00uy |]; [| 0x00uy; 0x0Auy |] ], feeds)

[<Fact>]
let ``utf-16 bytes decode to the text they carry`` () =
    let encoding = { Charset = Utf16Le; HasBom = false }

    let text = TextEncoding.decode encoding (wide "first")

    Assert.Equal("first", text)

[<Fact>]
let ``latin-1 bytes decode one character to the byte`` () =
    let encoding = { Charset = Latin1; HasBom = false }

    let text = TextEncoding.decode encoding [| 0x41uy; 0xE9uy |]

    Assert.Equal("Aé", text)
