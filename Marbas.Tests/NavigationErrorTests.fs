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
    match loadPanel fs side path state with
    | Ok s -> s
    | Error err -> failwith $"setup failed: %A{err}"

[<Fact>]
let ``entering a directory that vanished reports an error and keeps the panel`` () =
    let fs = fsWithVanishingDirectory ()
    let state = create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs |> setCursor Left 1
    fs.Directory.Delete @"C:\start\docs"

    Assert.Equal(Error(DirectoryNotFound(fp @"C:\start\docs")), enter fs state)

[<Fact>]
let ``failed navigation leaves the panel where it was`` () =
    let fs = fsWithVanishingDirectory ()
    let state = create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs |> setCursor Left 1
    fs.Directory.Delete @"C:\start\docs"

    match enter fs state with
    | Error _ -> Assert.Equal(fp @"C:\start", state.Left.Path)
    | Ok _ -> failwith "expected an error"

[<Fact>]
let ``UI reports listing failures and stays in the current directory`` () =
    let fs = fsWithVanishingDirectory ()
    let reported = ResizeArray<string>()

    use window =
        MainWindow.create
            { dependencies fs @"C:\start" with
                ReportError = fun _ -> reported.Add }

    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore

    let panelView = panel FilePanel.LeftId window
    let list = FilePanel.listView panelView
    list.SelectedItem <- 1
    fs.Directory.Delete @"C:\start\docs"
    press list Key.Enter

    Assert.Single reported |> ignore
    Assert.Contains(@"C:\start\docs", reported[0])
    Assert.Equal(@"C:\start", panelView.Title)
