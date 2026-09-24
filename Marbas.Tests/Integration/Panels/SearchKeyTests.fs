module Marbas.Tests.Integration.Panels.SearchKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs.AddFile(at "start/setup.exe", MockFileData "hi")
    fs

let private window () = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))

let private footerOf window id = (FilePanel.footer (panel id window)).Text

let private typeInto list (text: string) =
    text |> Seq.iter (fun character -> press list (Key character))

[<Fact>]
let ``typing moves the cursor to the first entry containing the text`` () =
    use window = window ()
    let list = leftList window

    typeInto list "set"

    Assert.Equal(4, list.SelectedItem.GetValueOrDefault 0)

[<Fact>]
let ``the panel footer shows what is being searched for`` () =
    use window = window ()
    let list = leftList window

    typeInto list "set"

    Assert.Equal("Search: set", footerOf window FilePanel.LeftId)

[<Fact>]
let ``backspace shortens the search and closing it restores the footer`` () =
    use window = window ()
    let list = leftList window
    typeInto list "se"

    press list Key.Backspace
    let shortened = footerOf window FilePanel.LeftId
    press list Key.Backspace

    Assert.Equal("Search: s", shortened)
    Assert.Contains("3 files", footerOf window FilePanel.LeftId)

[<Fact>]
let ``escape closes the search and leaves the cursor where it landed`` () =
    use window = window ()
    let list = leftList window
    typeInto list "readme"

    press list Key.Esc

    Assert.Equal(3, list.SelectedItem.GetValueOrDefault 0)
    Assert.Contains("3 files", footerOf window FilePanel.LeftId)

[<Fact>]
let ``a search is searched for in the panel that has the focus`` () =
    use window = window ()
    let right = rightList window
    right.SetFocus() |> ignore

    typeInto right "set"

    Assert.Equal("Search: set", footerOf window FilePanel.RightId)
    Assert.Contains("3 files", footerOf window FilePanel.LeftId)

[<Fact>]
let ``a command key closes the search and still does its job`` () =
    use window = window ()
    let list = leftList window
    typeInto list "set"

    press list KeyMap.selectionKeys.Head

    Assert.True(list.Source.IsMarked 4)
    Assert.Contains("1 selected", footerOf window FilePanel.LeftId)

[<Fact>]
let ``a space typed into an open search is part of the text, not a mark`` () =
    use window = window ()
    let list = leftList window
    typeInto list "s"

    press list KeyMap.spaceKey

    Assert.Equal("Search: s ", footerOf window FilePanel.LeftId)
    Assert.False(list.Source.IsMarked 1)
