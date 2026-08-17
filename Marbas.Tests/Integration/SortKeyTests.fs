module Marbas.Tests.Integration.SortKeyTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Views
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/big.txt", MockFileData(String.replicate 100 "x"))
    fs.AddFile(at "start/small.md", MockFileData "x")
    fs

let private names (list: ListView) =
    let source = list.Source.ToList()

    [ for i in 0 .. source.Count - 1 -> (string source[i]).Split(' ').[0] ]

[<Fact>]
let ``Ctrl+F6 sorts the focused panel by size`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    let list = leftList window
    let before = names list

    press list KeyMap.sizeSortKey

    Assert.Equal<string list>([ ".."; "big.txt"; "small.md" ], before)
    Assert.Equal<string list>([ ".."; "small.md"; "big.txt" ], names list)

[<Fact>]
let ``pressing the same sort key again reverses the panel`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    let list = leftList window
    press list KeyMap.sizeSortKey

    press list KeyMap.sizeSortKey

    Assert.Equal<string list>([ ".."; "big.txt"; "small.md" ], names list)

[<Fact>]
let ``the panel title tells which sort order is on`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    let list = leftList window

    press list KeyMap.extensionSortKey

    Assert.Equal(at "start" + " [ext ↑]", panelTitle FilePanel.LeftId window)

[<Fact>]
let ``sorting the focused panel leaves the other one alone`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    let list = leftList window

    press list KeyMap.sizeSortKey

    Assert.Equal<string list>([ ".."; "big.txt"; "small.md" ], names (rightList window))
    Assert.Equal(at "start" + " [name ↑]", panelTitle FilePanel.RightId window)
