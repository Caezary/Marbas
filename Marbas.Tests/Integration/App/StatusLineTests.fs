module Marbas.Tests.Integration.App.StatusLineTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Terminal.Gui.Views
open Xunit
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

let private statusText (window: Window) = (panel StatusLine.Id window).Text

let private startedWindow () = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))

[<Fact>]
let ``the window has a status line above the function key bar`` () =
    use window = createInitialized ()

    Assert.NotNull(panel StatusLine.Id window)

[<Fact>]
let ``the status line describes the entry under the cursor on start`` () =
    use window = startedWindow ()

    Assert.Contains("..", statusText window)

[<Fact>]
let ``moving the cursor updates the status line`` () =
    use window = startedWindow ()

    (leftList window).SelectedItem <- 2

    Assert.Contains("readme.txt", statusText window)
    Assert.Contains("2 bytes", statusText window)

[<Fact>]
let ``the status line follows the active panel`` () =
    use window = startedWindow ()
    (leftList window).SelectedItem <- 1
    let onLeftPanel = statusText window

    (panel FilePanel.RightId window).SetFocus() |> ignore

    Assert.Contains("docs", onLeftPanel)
    Assert.Contains("..", statusText window)
    Assert.DoesNotContain("docs", statusText window)
