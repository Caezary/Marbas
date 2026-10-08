module Marbas.Tests.Integration.App.MeasureKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.Ui.Format
open Marbas.Ui.Panels
open Marbas.Ui.App
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private Docs = 1

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "12345")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs

let private statusText (window: Window) = (panel StatusLine.Id window).Text

let private startedWindow () =
    focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))

[<Fact>]
let ``the view key on a directory puts its size in the panel`` () =
    use window = startedWindow ()
    let list = leftList window
    list.SelectedItem <- Docs

    press list Key.F3

    let row = string (list.Source.ToList()[Docs])

    Assert.DoesNotContain(EntrySize.DirectoryMark, row)
    Assert.Contains("5", row)

[<Fact>]
let ``the view key on a directory puts its size in the status line`` () =
    use window = startedWindow ()
    let list = leftList window
    list.SelectedItem <- Docs

    press list Key.F3

    Assert.Contains("5 bytes", statusText window)

[<Fact>]
let ``a directory nobody measured still wears the marker`` () =
    use window = startedWindow ()
    let list = leftList window

    let row = string (list.Source.ToList()[Docs])

    Assert.Contains(EntrySize.DirectoryMark, row)
