module Marbas.Tests.StatusLineTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Terminal.Gui.Views
open Xunit
open Marbas.Ui
open Marbas.Tests.TestHelpers

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")
    fs :> IFileSystem

let private statusText (window: Window) = (panel StatusLine.Id window).Text

let private focusedWindow () =
    let window = MainWindow.create (dependencies (mockFs ()) @"C:\start")
    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    window

[<Fact>]
let ``the window has a status line above the function key bar`` () =
    use window = createInitialized ()

    Assert.NotNull(panel StatusLine.Id window)

[<Fact>]
let ``the status line describes the entry under the cursor on start`` () =
    use window = focusedWindow ()

    Assert.Contains("..", statusText window)

[<Fact>]
let ``moving the cursor updates the status line`` () =
    use window = focusedWindow ()
    let list = FilePanel.listView (panel FilePanel.LeftId window)

    list.SelectedItem <- 2

    Assert.Contains("readme.txt", statusText window)
    Assert.Contains("2 bytes", statusText window)

[<Fact>]
let ``the status line follows the active panel`` () =
    use window = focusedWindow ()
    (FilePanel.listView (panel FilePanel.LeftId window)).SelectedItem <- 1
    Assert.Contains("docs", statusText window)

    (panel FilePanel.RightId window).SetFocus() |> ignore

    Assert.Contains("..", statusText window)
    Assert.DoesNotContain("docs", statusText window)
