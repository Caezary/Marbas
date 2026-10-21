module Marbas.Tests.Ui.Keys.KeySpellingTests

open Xunit
open Terminal.Gui.Drivers
open Terminal.Gui.Input
open Marbas.Guide
open Marbas.Ui.Keys

let private spelling key = KeySpelling.nameOf key |> KeyName.value

[<Theory>]
[<InlineData(KeyCode.CursorUp, "↑")>]
[<InlineData(KeyCode.CursorDown, "↓")>]
[<InlineData(KeyCode.CursorLeft, "←")>]
[<InlineData(KeyCode.CursorRight, "→")>]
[<InlineData(KeyCode.PageUp, "PgUp")>]
[<InlineData(KeyCode.PageDown, "PgDn")>]
[<InlineData(KeyCode.Home, "Home")>]
[<InlineData(KeyCode.End, "End")>]
[<InlineData(KeyCode.Insert, "Ins")>]
[<InlineData(KeyCode.Delete, "Del")>]
[<InlineData(KeyCode.Enter, "Enter")>]
[<InlineData(KeyCode.Esc, "Esc")>]
[<InlineData(KeyCode.Backspace, "Backspace")>]
[<InlineData(KeyCode.Tab, "Tab")>]
[<InlineData(KeyCode.Space, "Space")>]
let ``a key without a character is spelled by its name`` (code: KeyCode, name: string) =
    let key = Key code

    let spelled = spelling key

    Assert.Equal(name, spelled)

[<Theory>]
[<InlineData(KeyCode.F1, "F1")>]
[<InlineData(KeyCode.F2, "F2")>]
[<InlineData(KeyCode.F3, "F3")>]
[<InlineData(KeyCode.F4, "F4")>]
[<InlineData(KeyCode.F5, "F5")>]
[<InlineData(KeyCode.F6, "F6")>]
[<InlineData(KeyCode.F7, "F7")>]
[<InlineData(KeyCode.F8, "F8")>]
[<InlineData(KeyCode.F9, "F9")>]
[<InlineData(KeyCode.F10, "F10")>]
[<InlineData(KeyCode.F11, "F11")>]
[<InlineData(KeyCode.F12, "F12")>]
let ``a function key is spelled by its number`` (code: KeyCode, name: string) =
    let key = Key code

    let spelled = spelling key

    Assert.Equal(name, spelled)

[<Theory>]
[<InlineData('a', "A")>]
[<InlineData('z', "Z")>]
[<InlineData('0', "0")>]
[<InlineData('9', "9")>]
[<InlineData('+', "+")>]
[<InlineData('-', "-")>]
[<InlineData('*', "*")>]
[<InlineData('\\', "\\")>]
let ``a key with a character is spelled as its cap reads`` (pressed: char, name: string) =
    let key = Key pressed

    let spelled = spelling key

    Assert.Equal(name, spelled)

[<Fact>]
let ``Ctrl comes before the key`` () =
    let key = Key.F3.WithCtrl

    let spelled = spelling key

    Assert.Equal("Ctrl+F3", spelled)

[<Fact>]
let ``Alt comes before the key`` () =
    let key = Key.CursorLeft.WithAlt

    let spelled = spelling key

    Assert.Equal("Alt+←", spelled)

[<Fact>]
let ``Shift comes before the key`` () =
    let key = Key.Delete.WithShift

    let spelled = spelling key

    Assert.Equal("Shift+Del", spelled)

[<Fact>]
let ``Ctrl comes before Alt`` () =
    let key = Key.InsertChar.WithAlt.WithCtrl

    let spelled = spelling key

    Assert.Equal("Ctrl+Alt+Ins", spelled)

[<Fact>]
let ``Ctrl comes before Shift`` () =
    let key = Key.InsertChar.WithShift.WithCtrl

    let spelled = spelling key

    Assert.Equal("Ctrl+Shift+Ins", spelled)

[<Fact>]
let ``Alt comes before Shift`` () =
    let key = Key.E.WithShift.WithAlt

    let spelled = spelling key

    Assert.Equal("Alt+Shift+E", spelled)

[<Fact>]
let ``all three modifiers come in the order Ctrl, Alt, Shift`` () =
    let key = Key.F1.WithShift.WithAlt.WithCtrl

    let spelled = spelling key

    Assert.Equal("Ctrl+Alt+Shift+F1", spelled)

[<Fact>]
let ``a modifier before a digit keeps the digit`` () =
    let key = Key.D4.WithCtrl

    let spelled = spelling key

    Assert.Equal("Ctrl+4", spelled)

[<Fact>]
let ``a modifier before a sign keeps the sign`` () =
    let key = (Key '+').WithShift

    let spelled = spelling key

    Assert.Equal("Shift++", spelled)

[<Fact>]
let ``a letter typed with Shift is spelled with Shift`` () =
    let key = Key 'A'

    let spelled = spelling key

    Assert.Equal("Shift+A", spelled)

[<Fact>]
let ``a key the guide has no name for is spelled as Terminal.Gui names it`` () =
    let key = Key.PrintScreen

    let spelled = spelling key

    Assert.Equal("PrintScreen", spelled)
