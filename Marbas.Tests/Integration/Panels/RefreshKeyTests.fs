module Marbas.Tests.Integration.Panels.RefreshKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Views
open Marbas.Ui.Keys
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs

let private rows (list: ListView) =
    let source = list.Source.ToList()
    [ for i in 0 .. source.Count - 1 -> string source[i] ]

[<Fact>]
let ``Ctrl+R reloads the focused panel`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    let list = leftList window
    fs.AddFile(at "start/added.txt", MockFileData "new")
    let rowsBefore = rows list

    press list KeyMap.refreshKey

    Assert.DoesNotContain(rowsBefore, fun (row: string) -> row.StartsWith "added.txt")
    Assert.Contains(rows list, fun (row: string) -> row.StartsWith "added.txt")
