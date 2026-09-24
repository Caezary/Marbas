module Marbas.Tests.Integration.App.ListingErrorTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs

[<Fact>]
let ``the UI reports listing failures and stays in the current directory`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ReportError = fun report -> reported.Add report.Message }

    let panelView = panel FilePanel.LeftId window
    let list = FilePanel.listView panelView
    list.SelectedItem <- 1
    fs.Directory.Delete(at "start/docs")

    press list Key.Enter

    Assert.Contains(at "start/docs", Assert.Single reported)
    Assert.Equal(at "start", shownPath FilePanel.LeftId window)
