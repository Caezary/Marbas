module Marbas.Tests.Ui.Viewer.ViewerKeysTests

open Xunit
open Terminal.Gui.Input
open Marbas.Ui.Keys
open Marbas.Ui.Viewer
open Marbas.Ui.Viewer.ViewerKeys

[<Fact>]
let ``the arrows move by a line`` () =
    let commands = [ Key.CursorUp; Key.CursorDown ] |> List.map commandFor

    Assert.Equal<Command option list>([ Some LineUp; Some LineDown ], commands)

[<Fact>]
let ``the page keys move by a page`` () =
    let commands = [ Key.PageUp; Key.PageDown ] |> List.map commandFor

    Assert.Equal<Command option list>([ Some PageUp; Some PageDown ], commands)

[<Fact>]
let ``the side arrows move across the line`` () =
    let commands = [ Key.CursorLeft; Key.CursorRight ] |> List.map commandFor

    Assert.Equal<Command option list>([ Some ColumnLeft; Some ColumnRight ], commands)

[<Fact>]
let ``held control the side arrows move by a screen`` () =
    let commands = [ Key.CursorLeft.WithCtrl; Key.CursorRight.WithCtrl ] |> List.map commandFor

    Assert.Equal<Command option list>([ Some PageLeft; Some PageRight ], commands)

[<Fact>]
let ``home and end reach the ends of the file`` () =
    let commands =
        [ Key.Home; Key.End; Key.Home.WithCtrl; Key.End.WithCtrl ] |> List.map commandFor

    Assert.Equal<Command option list>([ Some ToStart; Some ToEnd; Some ToStart; Some ToEnd ], commands)

[<Fact>]
let ``every closing key closes`` () =
    let commands = [ Key.F3; Key.F10; Key.Esc ] |> List.map commandFor

    Assert.Equal<Command option list>([ Some Closed; Some Closed; Some Closed ], commands)

[<Fact>]
let ``the mode and the wrap have a key each`` () =
    let commands = [ Key.F2; Key.F4 ] |> List.map commandFor

    Assert.Equal<Command option list>([ Some WrapToggled; Some ModeToggled ], commands)

[<Fact>]
let ``a key the viewer does not use is nothing to it`` () =
    let command = commandFor (Key 'x')

    Assert.Equal(None, command)

[<Fact>]
let ``the bar has a slot for every function key`` () =
    let slots = bar TextMode Unwrapped

    Assert.Equal(List.length KeyMap.functionKeys, List.length slots)

[<Fact>]
let ``the bar offers the mode the viewer is not in`` () =
    let inText = bar TextMode Unwrapped
    let inHex = bar HexMode Unwrapped

    Assert.Equal("Hex", inText[3])
    Assert.Equal("Text", inHex[3])

[<Fact>]
let ``the bar offers to undo the wrap it is under`` () =
    let unwrapped = bar TextMode Unwrapped
    let wrapped = bar TextMode Wrapped

    Assert.Equal("Wrap", unwrapped[1])
    Assert.Equal("Unwrap", wrapped[1])

[<Fact>]
let ``the bar says which keys leave`` () =
    let slots = bar TextMode Unwrapped

    Assert.Equal("Quit", slots[2])
    Assert.Equal("Quit", slots[9])

[<Fact>]
let ``a slot with no command is left blank`` () =
    let slots = bar TextMode Unwrapped

    Assert.Equal("", slots[4])

[<Fact>]
let ``F8 asks for a code page`` () =
    let command = commandFor Key.F8

    Assert.Equal(Some CodePageAsked, command)

[<Fact>]
let ``the bar offers the code page on F8`` () =
    let slots = bar TextMode Unwrapped

    Assert.Equal("Code", slots[7])

[<Fact>]
let ``F7 asks for a search`` () =
    let command = commandFor Key.F7

    Assert.Equal(Some SearchAsked, command)

[<Fact>]
let ``the bar offers the search on F7 in text`` () =
    let slots = bar TextMode Unwrapped

    Assert.Equal("Search", slots[6])

[<Fact>]
let ``the bar offers the search in hex too`` () =
    let slots = bar HexMode Unwrapped

    Assert.Equal("Search", slots[6])

[<Fact>]
let ``Shift+F7 asks for the last search again`` () =
    let command = commandFor Key.F7.WithShift

    Assert.Equal(Some SearchRepeated, command)
