module Marbas.Tests.Domain.EnteredTextTests

open Xunit
open Marbas.Domain

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
[<InlineData("first\nsecond")>]
[<InlineData("first\rsecond")>]
let ``text that is blank or breaks a line is not entered text`` (raw: string) =
    let entered = EnteredText.tryCreate raw

    Assert.Equal(None, entered)

[<Theory>]
[<InlineData("*.txt")>]
[<InlineData(" leading and trailing ")>]
[<InlineData(@"C:\Program Files\app=1")>]
let ``entered text is kept exactly as it was typed`` (raw: string) =
    let entered = EnteredText.tryCreate raw |> Option.map EnteredText.value

    Assert.Equal(Some raw, entered)
