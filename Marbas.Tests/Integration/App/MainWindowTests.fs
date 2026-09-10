module Marbas.Tests.Integration.App.MainWindowTests

open System.Drawing
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Ui.App
open Marbas.Tests
open Marbas.Tests.Integration.Gui

[<Fact>]
let ``main window has application title`` () =
    use window = createWindow ()

    Assert.Equal("Marbas", window.Title)

[<Fact>]
let ``main window fills the whole screen`` () =
    use window = createWindow ()

    Assert.Equal(Dim.Fill(), window.Width)
    Assert.Equal(Dim.Fill(), window.Height)

[<Fact>]
let ``main window contains left and right panels`` () =
    use window = createWindow ()

    Assert.NotNull(panel FilePanel.LeftId window)
    Assert.NotNull(panel FilePanel.RightId window)

[<Theory>]
[<InlineData(80, 24)>]
[<InlineData(120, 30)>]
let ``panels split the window in half at any size`` (width: int) (height: int) =
    use window = createWindow ()

    window.Layout(Size(width, height)) |> ignore
    let content = window.Viewport
    let left = (panel FilePanel.LeftId window).Frame
    let right = (panel FilePanel.RightId window).Frame

    Assert.Equal(content.Width / 2, left.Width)
    Assert.Equal(content.Width - left.Width, right.Width)
    Assert.Equal(content.Height - 2, left.Height)
    Assert.Equal(content.Height - 2, right.Height)
    Assert.Equal(left.X + left.Width, right.X)

[<Fact>]
let ``quit command is bound to Ctrl+Q`` () =
    MainWindow.configureQuitKey ()

    let quitKey = Application.GetDefaultKey Command.Quit

    Assert.Equal(Key.Q.WithCtrl, quitKey)

[<Fact>]
let ``Esc closes whatever popup is open`` () =
    MainWindow.configureQuitKey ()

    let keys = Application.GetDefaultKeys Command.Quit

    Assert.Contains(KeyMap.cancelKey, keys)

[<Fact>]
let ``the directories the panels start in are put under watch`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(Root.at "start")
    let watched = ResizeArray<FileSystemPath list>()

    use _ =
        MainWindow.create
            { Fakes.dependencies fs (Root.at "start") with
                WatchLocations = fun paths _ -> watched.Add paths }

    Assert.Equal<FileSystemPath list list>([ [ FileSystemPath.create (Root.at "start") ] ], List.ofSeq watched)
