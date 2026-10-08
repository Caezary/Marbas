module Marbas.Tests.Integration.Format.LiteralTextTests

open System.Drawing
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Terminal.Gui.Input
open Xunit
open Marbas.Ui.Panels
open Marbas.Ui.Dialogs
open Marbas.Ui.App
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/_notes.txt", MockFileData "hi")
    fs :> IFileSystem

let private startedWindow () =
    let window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))
    window.Layout(Size(80, 24)) |> ignore
    window

[<Fact>]
let ``the status line shows the underscore of the name under the cursor`` () =
    use window = startedWindow ()

    (leftList window).SelectedItem <- 1

    Assert.Contains("_notes.txt", drawnText (panel StatusLine.Id window))

[<Fact>]
let ``the panel footer shows the underscore that is being searched for`` () =
    use window = startedWindow ()

    press (leftList window) (Key '_')
    press (leftList window) (Key 'n')

    Assert.Contains("Search: _n", drawnText (FilePanel.footer (panel FilePanel.LeftId window)))

[<Fact>]
let ``the progress dialog shows the underscore of the path it reports`` () =
    use dialog = ProgressDialog.create Shell.CopyTitle
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore

    ProgressDialog.report dialog (at "start/_notes.txt")

    Assert.Contains("_notes.txt", drawnText (ProgressDialog.line dialog))

[<Fact>]
let ``an underscore in the shown path claims no hot key from the panel`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/_sub")

    use window =
        focusedWindow (Fakes.dependencies (fs :> IFileSystem) (at "start/_sub"))

    window.Layout(Size(80, 24)) |> ignore

    let view = panel FilePanel.LeftId window

    Assert.Contains("_sub", view.Title)
    Assert.Equal(Key.Empty, view.HotKey)
