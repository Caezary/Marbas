module Marbas.Tests.Integration.Panels.FilePanelContentTests

open System.Drawing
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui.Panels
open Marbas.Ui.App
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

let private rule (panelView: View) =
    panelView.SubViews
    |> Seq.pick (function
        | :? Line as line -> Some line
        | _ -> None)

[<Fact>]
let ``a rule separates the listing from the footer`` () =
    use window = MainWindow.create (Fakes.dependencies (mockFs ()) (at "start"))
    window.BeginInit()
    window.EndInit()
    let panelView = panel FilePanel.LeftId window

    window.Layout(Size(80, 24)) |> ignore

    Assert.Equal(Orientation.Horizontal, (rule panelView).Orientation)
    Assert.Equal(panelView.Viewport.Height - 2, (rule panelView).Frame.Y)

[<Fact>]
let ``the listing gives up the two rows the rule and the footer take`` () =
    use window = MainWindow.create (Fakes.dependencies (mockFs ()) (at "start"))
    window.BeginInit()
    window.EndInit()
    let panelView = panel FilePanel.LeftId window
    let list = FilePanel.listView panelView

    window.Layout(Size(80, 24)) |> ignore

    Assert.Equal(panelView.Viewport.Height - 2, list.Viewport.Height)

[<Fact>]
let ``the footer counts the files and folders of the directory while nothing is marked`` () =
    use window = MainWindow.create (Fakes.dependencies (mockFs ()) (at "start"))

    let text = (FilePanel.footer (panel FilePanel.LeftId window)).Text

    Assert.Contains("1 file,", text)
    Assert.Contains("1 folder", text)
    Assert.Contains("2 bytes", text)

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
