module Marbas.Tests.Integration.FilePanelContentTests

open System.Drawing
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs :> IFileSystem

let private items panelView =
    let source = (FilePanel.listView panelView).Source.ToList()
    [ for i in 0 .. source.Count - 1 -> string source[i] ]

[<Fact>]
let ``both panels list the startup directory`` () =
    use window = MainWindow.create (Fakes.dependencies (mockFs ()) (at "start"))

    for id in [ FilePanel.LeftId; FilePanel.RightId ] do
        let names = items (panel id window)

        Assert.Contains("docs", names)
        Assert.Contains("readme.txt", names)

[<Fact>]
let ``panel listing starts with the parent entry for non-root paths`` () =
    use window = MainWindow.create (Fakes.dependencies (mockFs ()) (at "start"))

    let names = items (panel FilePanel.LeftId window)

    Assert.Equal("..", names.Head)

[<Fact>]
let ``after layout rows are formatted into aligned columns`` () =
    use window = MainWindow.create (Fakes.dependencies (mockFs ()) (at "start"))
    window.BeginInit()
    window.EndInit()
    let panelView = panel FilePanel.LeftId window
    let list = FilePanel.listView panelView

    window.Layout(Size(80, 24)) |> ignore
    let rows = items panelView

    Assert.NotEmpty rows
    Assert.All(rows, fun row -> Assert.Equal(list.Viewport.Width, row.Length))
    Assert.Contains(rows, fun (row: string) -> row.StartsWith "docs" && row.Contains "<DIR>")

[<Fact>]
let ``enter key on a directory navigates the focused panel`` () =
    use window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))
    let panelView = panel FilePanel.LeftId window
    let list = FilePanel.listView panelView
    list.SelectedItem <- 1

    press list Terminal.Gui.Input.Key.Enter

    Assert.Equal(at "start/docs", shownPath FilePanel.LeftId window)
    Assert.StartsWith("..", (items panelView).Head)

[<Fact>]
let ``panel titles show the current path`` () =
    use window = MainWindow.create (Fakes.dependencies (mockFs ()) (at "start"))

    Assert.Equal(at "start", shownPath FilePanel.LeftId window)
    Assert.Equal(at "start", shownPath FilePanel.RightId window)
