module Marbas.Tests.Ui.Help.HelpKeysTests

open Xunit
open Terminal.Gui.Drivers
open Terminal.Gui.Input
open Marbas.Ui.Help

[<Theory>]
[<InlineData(KeyCode.CursorUp, "LineUp")>]
[<InlineData(KeyCode.CursorDown, "LineDown")>]
[<InlineData(KeyCode.PageUp, "PageUp")>]
[<InlineData(KeyCode.PageDown, "PageDown")>]
[<InlineData(KeyCode.Home, "ToStart")>]
[<InlineData(KeyCode.End, "ToEnd")>]
[<InlineData(KeyCode.Tab, "NextLink")>]
[<InlineData(KeyCode.Enter, "Follow")>]
[<InlineData(KeyCode.Backspace, "Back")>]
[<InlineData(KeyCode.F7, "SearchAsked")>]
[<InlineData(KeyCode.Esc, "Closed")>]
[<InlineData(KeyCode.F10, "Closed")>]
let ``a key of the help stands for its command`` (code: KeyCode, command: string) =
    let key = Key code

    let found = HelpKeys.commandFor key

    Assert.Equal(Some command, found |> Option.map string)

[<Fact>]
let ``Shift+Tab goes to the previous link`` () =
    let key = Key.Tab.WithShift

    let found = HelpKeys.commandFor key

    Assert.Equal(Some HelpKeys.PreviousLink, found)

[<Fact>]
let ``Alt+← goes back`` () =
    let key = Key.CursorLeft.WithAlt

    let found = HelpKeys.commandFor key

    Assert.Equal(Some HelpKeys.Back, found)

[<Fact>]
let ``Shift+F7 repeats the search`` () =
    let key = Key.F7.WithShift

    let found = HelpKeys.commandFor key

    Assert.Equal(Some HelpKeys.SearchRepeated, found)

[<Fact>]
let ``a key the help does not bind stands for nothing`` () =
    let key = Key.F5

    let found = HelpKeys.commandFor key

    Assert.Equal(None, found)

[<Fact>]
let ``the bar names F7 and F10`` () =
    let labels = HelpKeys.bar

    Assert.Equal<string list>([ ""; ""; ""; ""; ""; ""; "Search"; ""; ""; "Quit"; ""; "" ], labels)
