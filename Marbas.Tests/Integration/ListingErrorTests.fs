module Marbas.Tests.Integration.ListingErrorTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")
    fs

[<Fact>]
let ``the UI reports listing failures and stays in the current directory`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()

    use window =
        focusedWindow
            { Fakes.dependencies fs @"C:\start" with
                ReportError = fun _ -> reported.Add }

    let panelView = panel FilePanel.LeftId window
    let list = FilePanel.listView panelView
    list.SelectedItem <- 1
    fs.Directory.Delete @"C:\start\docs"

    press list Key.Enter

    Assert.Contains(@"C:\start\docs", Assert.Single reported)
    Assert.Equal(@"C:\start", panelView.Title)
