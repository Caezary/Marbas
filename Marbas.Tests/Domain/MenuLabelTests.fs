module Marbas.Tests.Domain.MenuLabelTests

open Xunit
open Marbas.Domain

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
[<InlineData("first\nsecond")>]
[<InlineData("first\rsecond")>]
let ``text that is blank or breaks a line is no label`` (raw: string) =
    let label = MenuLabel.tryCreate raw

    Assert.Equal(None, label)

[<Fact>]
let ``a label is trimmed`` () =
    let label = MenuLabel.tryCreate "  Compare with the other panel  " |> Option.map MenuLabel.value

    Assert.Equal(Some "Compare with the other panel", label)
