module Marbas.Tests.NavigationErrorTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State
open Marbas.State.AppState
open Marbas.Ui
open Marbas.Tests.TestHelpers

let private fp = FileSystemPath.create

let private fsWithVanishingDirectory () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")
    fs

let private loaded side path (fs: IFileSystem) state =
    loadPanel fs side path state |> Expect.ok

[<Fact>]
let ``entering a directory that vanished reports an error and keeps the panel`` () =
    let fs = fsWithVanishingDirectory ()
    let state = create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs |> setCursor Left 1
    fs.Directory.Delete @"C:\start\docs"

    let result = enter fs state

    Assert.Equal(Error(DirectoryNotFound(fp @"C:\start\docs")), result)

[<Fact>]
let ``failed navigation leaves the panel where it was`` () =
    let fs = fsWithVanishingDirectory ()
    let state = create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs |> setCursor Left 1
    fs.Directory.Delete @"C:\start\docs"

    let failure = enter fs state |> Expect.error

    Assert.Equal(DirectoryNotFound(fp @"C:\start\docs"), failure)
    Assert.Equal(fp @"C:\start", state.Left.Path)

[<Fact>]
let ``UI reports listing failures and stays in the current directory`` () =
    let fs = fsWithVanishingDirectory ()
    let reported = ResizeArray<string>()

    use window =
        focusedWindow
            { dependencies fs @"C:\start" with
                ReportError = fun _ -> reported.Add }

    let panelView = panel FilePanel.LeftId window
    let list = FilePanel.listView panelView
    list.SelectedItem <- 1
    fs.Directory.Delete @"C:\start\docs"

    press list Key.Enter

    Assert.Single reported |> ignore
    Assert.Contains(@"C:\start\docs", reported[0])
    Assert.Equal(@"C:\start", panelView.Title)
