module Marbas.Tests.FilePanelContentTests

open System.Drawing
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Ui
open Marbas.Tests.TestHelpers

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")
    fs :> IFileSystem

let private items panelView =
    let list = FilePanel.listView panelView
    let source = list.Source.ToList()
    [ for i in 0 .. source.Count - 1 -> string source[i] ]

[<Fact>]
let ``both panels list the startup directory`` () =
    use window = MainWindow.create (dependencies (mockFs ()) @"C:\start")

    for id in [ FilePanel.LeftId; FilePanel.RightId ] do
        let names = items (panel id window)
        Assert.Contains("docs", names)
        Assert.Contains("readme.txt", names)

[<Fact>]
let ``panel listing starts with the parent entry for non-root paths`` () =
    use window = MainWindow.create (dependencies (mockFs ()) @"C:\start")

    Assert.Equal("..", items (panel FilePanel.LeftId window) |> List.head)

[<Fact>]
let ``after layout rows are formatted into aligned columns`` () =
    use window = MainWindow.create (dependencies (mockFs ()) @"C:\start")
    window.BeginInit()
    window.EndInit()
    window.Layout(Size(80, 24)) |> ignore

    let panelView = panel FilePanel.LeftId window
    let list = FilePanel.listView panelView
    let rows = items panelView

    Assert.NotEmpty rows
    Assert.All(rows, fun row -> Assert.Equal(list.Viewport.Width, row.Length))
    Assert.Contains(rows, fun (row: string) -> row.StartsWith "docs" && row.Contains "<DIR>")

[<Fact>]
let ``enter key on a directory navigates the focused panel`` () =
    use window = MainWindow.create (dependencies (mockFs ()) @"C:\start")
    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore

    let panelView = panel FilePanel.LeftId window
    let list = FilePanel.listView panelView
    list.SelectedItem <- 1
    press list Terminal.Gui.Input.Key.Enter

    Assert.Equal(@"C:\start\docs", panelView.Title)
    Assert.StartsWith("..", items panelView |> List.head)

[<Fact>]
let ``panel titles show the current path`` () =
    use window = MainWindow.create (dependencies (mockFs ()) @"C:\start")

    Assert.Equal(@"C:\start", (panel FilePanel.LeftId window).Title)
    Assert.Equal(@"C:\start", (panel FilePanel.RightId window).Title)
