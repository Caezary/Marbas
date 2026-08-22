module Marbas.Tests.Ui.PrintableTextTests

open System
open Xunit
open Marbas.Ui

[<Fact>]
let ``ordinary text is left alone`` () =
    let shown = PrintableText.describe "{ \"name\": \"zażółć\" }"

    Assert.Equal("{ \"name\": \"zażółć\" }", shown)

[<Fact>]
let ``a tab runs to the next stop`` () =
    let shown = PrintableText.describe "ab\tc"

    Assert.Equal("ab      c", shown)

[<Fact>]
let ``a tab on a stop runs a whole width`` () =
    let shown = PrintableText.describe "12345678\tx"

    Assert.Equal("12345678        x", shown)

[<Fact>]
let ``a control character stands in for itself`` () =
    let raw = String [| 'a'; char 0; 'b'; char 27; 'c' |]

    let shown = PrintableText.describe raw

    Assert.Equal("a.b.c", shown)

[<Fact>]
let ``nothing shows as nothing`` () =
    let shown = PrintableText.describe ""

    Assert.Equal("", shown)
