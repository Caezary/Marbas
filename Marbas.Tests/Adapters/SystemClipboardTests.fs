module Marbas.Tests.Adapters.SystemClipboardTests

open Xunit
open Marbas.Domain
open Marbas.Adapters
open Marbas.Tests

[<Fact>]
let ``Unix refuses every text, since Marbas cannot reach its clipboard yet`` () =
    let text = ClipboardText.tryCreate [ "notes.txt" ] |> Expect.some

    let answer = (SystemClipboard.forPlatform Unix).SetText text

    Assert.Equal(Error ClipboardUnavailable, answer)

[<Fact>]
let ``Unix refuses to be read as well`` () =
    let answer = (SystemClipboard.forPlatform Unix).GetText()

    Assert.Equal(Error ClipboardUnavailable, answer)
