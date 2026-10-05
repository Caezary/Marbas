module Marbas.Tests.Domain.SearchPatternTests

open Xunit
open Marbas.Domain
open Marbas.Tests

let private doesNotParse error =
    match error with
    | PatternDoesNotParse _ -> true
    | PatternNotSupported _ -> false

let private notSupported error =
    match error with
    | PatternNotSupported _ -> true
    | PatternDoesNotParse _ -> false

[<Theory>]
[<InlineData("needle")>]
[<InlineData("ne+dle")>]
[<InlineData(@"^line \d+$")>]
[<InlineData("(a|b)[c-z]*")>]
let ``a pattern that parses is kept as it was typed`` (raw: string) =
    let pattern = SearchPattern.tryCreate raw

    Assert.Equal(raw, SearchPattern.value (Expect.ok pattern))

[<Theory>]
[<InlineData("(")>]
[<InlineData("[a-")>]
[<InlineData("a{2,1}")>]
[<InlineData("*")>]
let ``a pattern that does not parse is refused`` (raw: string) =
    let pattern = SearchPattern.tryCreate raw

    let refusal = Expect.error pattern

    Assert.True(doesNotParse refusal, $"%A{refusal}")

[<Theory>]
[<InlineData(@"(a)\1")>]
[<InlineData("a(?=b)")>]
[<InlineData("(?<=a)b")>]
[<InlineData("(?>a)")>]
let ``a pattern the linear engine cannot run is refused`` (raw: string) =
    let pattern = SearchPattern.tryCreate raw

    let refusal = Expect.error pattern

    Assert.True(notSupported refusal, $"%A{refusal}")

[<Fact>]
let ``a refusal carries what the parser said`` () =
    let pattern = SearchPattern.tryCreate "("

    let refusal = Expect.error pattern

    Assert.NotEmpty(PatternError.reason refusal)

[<Fact>]
let ``a pattern ignoring case finds either case`` () =
    let pattern = SearchPattern.tryCreate "NEEDLE" |> Expect.ok

    let found = (SearchPattern.regex IgnoringCase pattern).IsMatch "a needle"

    Assert.True found

[<Fact>]
let ``a pattern minding case finds only its own`` () =
    let pattern = SearchPattern.tryCreate "NEEDLE" |> Expect.ok

    let found = (SearchPattern.regex MatchingCase pattern).IsMatch "a needle"

    Assert.False found
