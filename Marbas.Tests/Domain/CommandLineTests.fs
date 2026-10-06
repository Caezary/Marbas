module Marbas.Tests.Domain.CommandLineTests

open Xunit
open Marbas.Domain

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
[<InlineData("first\nsecond")>]
[<InlineData("first\rsecond")>]
let ``text that is blank or breaks a line is no command line`` (raw: string) =
    let line = CommandLine.tryCreate raw

    Assert.Equal(None, line)

[<Fact>]
let ``a command line is trimmed and otherwise kept as it was typed`` () =
    let line = CommandLine.tryCreate "  code --diff %Lp %Rp=1  " |> Option.map CommandLine.value

    Assert.Equal(Some "code --diff %Lp %Rp=1", line)
