module Marbas.Tests.Viewing.TextEncodingTests

open System.Text
open Xunit
open Marbas.Viewing

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
        TextEncoding.charsets
        |> List.map (fun charset -> TextEncoding.name { Charset = charset; Bom = WithoutBom })

    Assert.Equal<string list>(
        [ "UTF-8"
          "UTF-16 LE"
          "UTF-16 BE"
          "Latin-1"
          "Windows-1250"
          "CP852"
          "ISO-8859-2"
          "Windows-1252"
          "CP437" ],
        names
    )

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
        TextEncoding.charsets
        |> List.map (fun charset -> TextEncoding.unitSize { Charset = charset; Bom = WithoutBom })

    Assert.Equal<int list>([ 1; 2; 2; 1; 1; 1; 1; 1; 1 ], sizes)

[<Fact>]
let ``a line feed is one byte in utf-8 and two in utf-16`` () =
    let feeds =
        [ Utf8; Utf16Le; Utf16Be ]
        |> List.map (fun charset -> TextEncoding.lineFeed { Charset = charset; Bom = WithoutBom })

    Assert.Equal<byte array list>([ [| 0x0Auy |]; [| 0x0Auy; 0x00uy |]; [| 0x00uy; 0x0Auy |] ], feeds)

[<Fact>]
let ``a single-byte code page ends a line with one line feed`` () =
    let feeds =
        [ Latin1; Windows1250; Cp852; Iso88592; Windows1252; Cp437 ]
        |> List.map (fun charset -> TextEncoding.lineFeed { Charset = charset; Bom = WithoutBom })

    Assert.All(feeds, (fun feed -> Assert.Equal<byte array>([| 0x0Auy |], feed)))

[<Fact>]
let ``the same bytes read differently in every code page`` () =
    let probe = [| 0x80uy; 0xA5uy; 0xB1uy; 0xB9uy |]

    let texts =
        TextEncoding.charsets
        |> List.map (fun charset -> TextEncoding.decode { Charset = charset; Bom = WithoutBom } probe)

    Assert.Equal<string list>([ "����"; "ꖀ릱"; "肥놹"; "\u0080¥±¹"; "€Ą±ą"; "Çą▒╣"; "\u0080Ľąš"; "€¥±¹"; "ÇÑ▒╣" ], texts)

[<Fact>]
let ``a chosen charset keeps the mark the file starts with`` () =
    let probe = Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] (bytes "first")

    let encoding = TextEncoding.chosen Utf8 probe

    Assert.Equal({ Charset = Utf8; Bom = WithBom }, encoding)

[<Fact>]
let ``a chosen charset reads another charset's mark as content`` () =
    let probe = Array.append [| 0xFFuy; 0xFEuy |] (wide "first")

    let encoding = TextEncoding.chosen Utf8 probe

    Assert.Equal({ Charset = Utf8; Bom = WithoutBom }, encoding)

[<Fact>]
let ``a single-byte code page has no mark`` () =
    let probe = Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] (bytes "first")

    let encoding = TextEncoding.chosen Windows1250 probe

    Assert.Equal(
        { Charset = Windows1250
          Bom = WithoutBom },
        encoding
    )

[<Fact>]
let ``a single-byte code page takes one byte to the character`` () =
    let encoding = { Charset = Cp852; Bom = WithoutBom }

    let length = TextEncoding.characterLengthAt encoding [| 0xA5uy; 0x41uy |] 0

    Assert.Equal(Some 1, length)

[<Theory>]
[<InlineData(0, 1)>]
[<InlineData(1, 2)>]
[<InlineData(3, 3)>]
[<InlineData(6, 4)>]
let ``a utf-8 character is as long as its lead byte says`` (position: int, expected: int) =
    let encoding = { Charset = Utf8; Bom = WithoutBom }
    let probe = Encoding.UTF8.GetBytes "Aż€😀"

    let length = TextEncoding.characterLengthAt encoding probe position

    Assert.Equal(Some expected, length)

[<Theory>]
[<InlineData(2)>]
[<InlineData(4)>]
let ``a utf-8 continuation byte starts no character`` (position: int) =
    let encoding = { Charset = Utf8; Bom = WithoutBom }
    let probe = Encoding.UTF8.GetBytes "Aż€"

    let length = TextEncoding.characterLengthAt encoding probe position

    Assert.Equal(None, length)

[<Fact>]
let ``a utf-8 character cut off by the end is no character`` () =
    let encoding = { Charset = Utf8; Bom = WithoutBom }
    let probe = (Encoding.UTF8.GetBytes "A€")[..2]

    let length = TextEncoding.characterLengthAt encoding probe 1

    Assert.Equal(None, length)

[<Theory>]
[<InlineData(0, 2)>]
[<InlineData(2, 4)>]
let ``a utf-16 character is a unit, or two for a surrogate pair`` (position: int, expected: int) =
    let encoding = { Charset = Utf16Le; Bom = WithoutBom }
    let probe = wide "A😀"

    let length = TextEncoding.characterLengthAt encoding probe position

    Assert.Equal(Some expected, length)

[<Fact>]
let ``a big-endian surrogate pair is read in its own byte order`` () =
    let encoding = { Charset = Utf16Be; Bom = WithoutBom }
    let probe = wideBigEndian "😀"

    let length = TextEncoding.characterLengthAt encoding probe 0

    Assert.Equal(Some 4, length)

[<Fact>]
let ``a utf-16 low surrogate on its own starts no character`` () =
    let encoding = { Charset = Utf16Le; Bom = WithoutBom }
    let probe = wide "😀"

    let length = TextEncoding.characterLengthAt encoding probe 2

    Assert.Equal(None, length)

[<Fact>]
let ``a utf-16 unit cut off by the end is no character`` () =
    let encoding = { Charset = Utf16Le; Bom = WithoutBom }
    let probe = (wide "AB")[..2]

    let length = TextEncoding.characterLengthAt encoding probe 2

    Assert.Equal(None, length)

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

    let _, content = TextEncoding.examine probe

    Assert.Equal<Content>(Binary, content)

[<Fact>]
let ``plain text is not binary`` () =
    let probe = bytes "{ \"name\": \"value\" }"

    let _, content = TextEncoding.examine probe

    Assert.Equal<Content>(Textual, content)

[<Fact>]
let ``marked utf-16 is text, nul bytes and all`` () =
    let probe = Array.append [| 0xFFuy; 0xFEuy |] (wide "first")

    let _, content = TextEncoding.examine probe

    Assert.Equal<Content>(Textual, content)

[<Fact>]
let ``unmarked utf-16 is text, nul bytes and all`` () =
    let probe = wide "first line of the file"

    let _, content = TextEncoding.examine probe

    Assert.Equal<Content>(Textual, content)

[<Fact>]
let ``one examination answers the encoding beside whether it is text`` () =
    let probe = Array.append (bytes "MZ") [| 0x00uy; 0x01uy; 0x02uy |]

    let examined = TextEncoding.examine probe

    Assert.Equal(({ Charset = Latin1; Bom = WithoutBom }, Binary), examined)

[<Fact>]
let ``what is detected is the encoding an examination answers`` () =
    let probe = wide "first line of the file"

    let encoding = TextEncoding.detect probe

    Assert.Equal(fst (TextEncoding.examine probe), encoding)

[<Fact>]
let ``unmarked utf-16 of an odd length is recognised by the pairs it holds`` () =
    let probe = Array.append (wide "first") [| 0x21uy |]

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf16Le; Bom = WithoutBom }, encoding)

[<Fact>]
let ``zero bytes in no more than half the pairs are not utf-16`` () =
    let probe = [| 0x41uy; 0x00uy; 0x42uy; 0x43uy |]

    let examined = TextEncoding.examine probe

    Assert.Equal(({ Charset = Latin1; Bom = WithoutBom }, Binary), examined)

[<Fact>]
let ``a zero byte on the other side of one pair keeps the rest from counting as utf-16`` () =
    let probe = [| 0x41uy; 0x00uy; 0x42uy; 0x00uy; 0x00uy; 0x43uy |]

    let examined = TextEncoding.examine probe

    Assert.Equal(({ Charset = Latin1; Bom = WithoutBom }, Binary), examined)

[<Fact>]
let ``a single zero byte is too short to be utf-16`` () =
    let probe = [| 0x00uy |]

    let examined = TextEncoding.examine probe

    Assert.Equal(({ Charset = Latin1; Bom = WithoutBom }, Binary), examined)

[<Theory>]
[<InlineData("é")>]
[<InlineData("€")>]
[<InlineData("😀")>]
[<InlineData("aé€😀z")>]
let ``a sequence of two, three or four bytes is utf-8`` (text: string) =
    let probe = Encoding.UTF8.GetBytes text

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Utf8; Bom = WithoutBom }, encoding)

[<Theory>]
[<InlineData(0xC3, 0x41, 0x41, 0x41)>]
[<InlineData(0xE2, 0x82, 0x41, 0x41)>]
[<InlineData(0xF0, 0x9F, 0x98, 0x41)>]
[<InlineData(0xF0, 0x41, 0x98, 0x80)>]
let ``a sequence one of whose later bytes continues nothing is not utf-8``
    (lead: int, second: int, third: int, fourth: int)
    =
    let probe = [| byte lead; byte second; byte third; byte fourth; 0x41uy |]

    let encoding = TextEncoding.detect probe

    Assert.Equal({ Charset = Latin1; Bom = WithoutBom }, encoding)
