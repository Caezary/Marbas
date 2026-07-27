module Marbas.Tests.RefreshTests

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

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")
    fs

let private loaded side path (fs: IFileSystem) state =
    match loadPanel fs side path state with
    | Ok s -> s
    | Error err -> failwith $"setup failed: %A{err}"

let private names entries = entries |> List.map (fun (e: Entry) -> e.Name)

[<Fact>]
let ``refresh picks up entries created after the panel was loaded`` () =
    let fs = mockFs ()
    let state = create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs
    fs.AddFile(@"C:\start\added.txt", MockFileData "new")

    match refresh fs state with
    | Ok updated -> Assert.Contains("added.txt", names updated.Left.Entries)
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``refresh keeps the cursor on the same entry when its index shifts`` () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs |> setCursor Left 2

    Assert.Equal("readme.txt", state.Left.Entries[2].Name)
    fs.AddDirectory @"C:\start\aaa"

    match refresh fs state with
    | Ok updated ->
        Assert.Equal<string list>([ ".."; "aaa"; "docs"; "readme.txt" ], names updated.Left.Entries)
        Assert.Equal(3, updated.Left.Cursor)
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``refresh falls back to the first entry when the cursor entry is gone`` () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs |> setCursor Left 2

    fs.File.Delete @"C:\start\readme.txt"

    match refresh fs state with
    | Ok updated -> Assert.Equal(0, updated.Left.Cursor)
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``refresh only reloads the active panel`` () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start")
        |> loaded Left (fp @"C:\start") fs
        |> loaded Right (fp @"C:\start") fs
        |> activate Right

    fs.AddFile(@"C:\start\added.txt", MockFileData "new")

    match refresh fs state with
    | Ok updated ->
        Assert.Contains("added.txt", names updated.Right.Entries)
        Assert.DoesNotContain("added.txt", names updated.Left.Entries)
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``refreshing a directory that vanished reports an error`` () =
    let fs = mockFs ()
    let state = create (fp @"C:\start\docs") |> loaded Left (fp @"C:\start\docs") fs
    fs.Directory.Delete @"C:\start\docs"

    Assert.Equal(Error(DirectoryNotFound(fp @"C:\start\docs")), refresh fs state)

[<Fact>]
let ``Ctrl+R reloads the focused panel`` () =
    let fs = mockFs ()

    use window =
        MainWindow.create (dependencies fs @"C:\start")

    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore

    let panelView = panel FilePanel.LeftId window
    let list = FilePanel.listView panelView
    fs.AddFile(@"C:\start\added.txt", MockFileData "new")

    let rowsBefore =
        let source = list.Source.ToList()
        [ for i in 0 .. source.Count - 1 -> string source[i] ]

    Assert.DoesNotContain(rowsBefore, fun (row: string) -> row.StartsWith "added.txt")

    press list MainWindow.refreshKey

    let rowsAfter =
        let source = list.Source.ToList()
        [ for i in 0 .. source.Count - 1 -> string source[i] ]

    Assert.Contains(rowsAfter, fun (row: string) -> row.StartsWith "added.txt")
