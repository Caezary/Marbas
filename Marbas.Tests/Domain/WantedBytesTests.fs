module Marbas.Tests.Domain.WantedBytesTests

open Xunit
open Marbas.Domain
open Marbas.Tests

let private bytesOf raw =
    WantedBytes.tryParse raw |> Expect.ok |> WantedBytes.value

[<Theory>]
[<InlineData("4D 5A")>]
[<InlineData("4d 5a")>]
[<InlineData("4D5A")>]
[<InlineData("  4D   5A  ")>]
[<InlineData("4D\t5A")>]
let ``pairs of hexadecimal digits are bytes, spaced or not`` (raw: string) =
    let bytes = bytesOf raw

    Assert.Equal<byte array>([| 0x4Duy; 0x5Auy |], bytes)

[<Fact>]
let ``a single digit is a byte of its own`` () =
    let bytes = bytesOf "A 0 FF"

    Assert.Equal<byte array>([| 0x0Auy; 0x00uy; 0xFFuy |], bytes)

[<Fact>]
let ``runs of pairs and single digits mix`` () =
    let bytes = bytesOf "4D5A 9 0"

    Assert.Equal<byte array>([| 0x4Duy; 0x5Auy; 0x09uy; 0x00uy |], bytes)

[<Theory>]
[<InlineData("4G", "4G")>]
[<InlineData("MZ", "MZ")>]
[<InlineData("4D 0x5A", "0x5A")>]
[<InlineData("4D-5A", "4D-5A")>]
let ``a token that is not hexadecimal is refused by name`` (raw: string) (token: string) =
    let refusal = WantedBytes.tryParse raw |> Expect.error

    Assert.Equal(NotAByte token, refusal)

[<Fact>]
let ``an odd run of digits longer than one is refused`` () =
    let refusal = WantedBytes.tryParse "4D5" |> Expect.error

    Assert.Equal(NotAByte "4D5", refusal)

[<Fact>]
let ``a field of spaces holds no bytes`` () =
    let refusal = WantedBytes.tryParse "   " |> Expect.error

    Assert.Equal(NoBytes, refusal)

[<Fact>]
let ``bytes are spelled as pairs, upper case, spaced`` () =
    let spelled = WantedBytes.tryParse "4d5a 9" |> Expect.ok |> WantedBytes.spelled

    Assert.Equal("4D 5A 09", spelled)
