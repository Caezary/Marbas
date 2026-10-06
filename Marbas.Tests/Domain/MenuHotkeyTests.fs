module Marbas.Tests.Domain.MenuHotkeyTests

open Xunit
open Marbas.Domain

[<Theory>]
[<InlineData("e", 'E')>]
[<InlineData("E", 'E')>]
[<InlineData("7", '7')>]
[<InlineData(" z ", 'Z')>]
[<InlineData("ż", 'Ż')>]
let ``one letter or digit is a hotkey, kept in capitals`` (raw: string, expected: char) =
    let hotkey = MenuHotkey.tryCreate raw |> Option.map MenuHotkey.value

    Assert.Equal(Some expected, hotkey)

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
[<InlineData("ab")>]
[<InlineData("%")>]
[<InlineData("-")>]
[<InlineData("\t")>]
let ``anything but one letter or digit is no hotkey`` (raw: string) =
    let hotkey = MenuHotkey.tryCreate raw

    Assert.Equal(None, hotkey)

[<Fact>]
let ``a letter is the same hotkey whatever its case`` () =
    let lower = MenuHotkey.tryCreate "q"

    let upper = MenuHotkey.tryCreate "Q"

    Assert.Equal(upper, lower)
