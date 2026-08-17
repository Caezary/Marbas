module Marbas.Tests.Integration.AppViewTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs :> IFileSystem

let private loaded (fs: IFileSystem) =
    create (fp (at "start")) |> loadPanel fs Left (fp (at "start")) |> Expect.ok

let private rows panelView =
    let list = FilePanel.listView panelView
    let source = list.Source.ToList()
    [ for i in 0 .. source.Count - 1 -> string source[i] ]

[<Fact>]
let ``the view holds both panels and the status line`` () =
    let view = AppView.create ()
    use _window = view.Window

    Assert.NotNull(panel FilePanel.LeftId view.Window)
    Assert.NotNull(panel FilePanel.RightId view.Window)
    Assert.NotNull(panel StatusLine.Id view.Window)

[<Fact>]
let ``panels are paired with the side they show`` () =
    let view = AppView.create ()
    use _window = view.Window

    let ids = AppView.panels view |> List.map (fun (side, p) -> side, p.Id)

    Assert.Equal<(PanelSide * string) list>([ Left, FilePanel.LeftId; Right, FilePanel.RightId ], ids)

[<Fact>]
let ``rendering fills the panel of the side it belongs to`` () =
    let view = AppView.create ()
    use _window = view.Window
    let state = loaded (mockFs ())

    AppView.render view state

    let left = panel FilePanel.LeftId view.Window
    Assert.Equal(at "start", shownPath FilePanel.LeftId view.Window)
    Assert.Contains("readme.txt", rows left)
    Assert.Empty(rows (panel FilePanel.RightId view.Window))

[<Fact>]
let ``rendering describes the cursor entry of the active panel`` () =
    let view = AppView.create ()
    use _window = view.Window
    let state = loaded (mockFs ()) |> setCursor Left 2

    AppView.render view state

    let text = (panel StatusLine.Id view.Window).Text
    Assert.Contains("readme.txt", text)
    Assert.Contains("2 bytes", text)
