module Marbas.Tests.NavigationTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.State
open Marbas.State.AppState

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs\inner"
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")
    fs :> IFileSystem

let private loaded side path fs state =
    match loadPanel fs side path state with
    | Ok s -> s
    | Error err -> failwith $"setup failed: %A{err}"

let private names entries = entries |> List.map (fun (e: Marbas.FileSystem.DirectoryListing.Entry) -> e.Name)

[<Fact>]
let ``cursor is reset to zero after loading a panel`` () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start")
        |> loaded Left (fp @"C:\start") fs
        |> setCursor Left 2
        |> loaded Left (fp @"C:\start\docs") fs

    Assert.Equal(0, state.Left.Cursor)

[<Fact>]
let ``enter descends into the directory under the cursor`` () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs |> setCursor Left 1

    match enter fs state with
    | Ok updated ->
        Assert.Equal(fp @"C:\start\docs", updated.Left.Path)
        Assert.Equal<string list>([ ".."; "inner" ], names updated.Left.Entries)
        Assert.Equal(0, updated.Left.Cursor)
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``enter on the parent entry goes up with cursor on the directory left behind`` () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start\docs") |> loaded Left (fp @"C:\start\docs") fs |> setCursor Left 0

    match enter fs state with
    | Ok updated ->
        Assert.Equal(fp @"C:\start", updated.Left.Path)
        Assert.Equal<string list>([ ".."; "docs"; "readme.txt" ], names updated.Left.Entries)
        Assert.Equal(1, updated.Left.Cursor)
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``enter on a file leaves the state unchanged`` () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs |> setCursor Left 2

    Assert.Equal(Ok state, enter fs state)

[<Fact>]
let ``enter acts on the active panel only`` () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start")
        |> loaded Left (fp @"C:\start") fs
        |> loaded Right (fp @"C:\start") fs
        |> activate Right
        |> setCursor Right 1

    match enter fs state with
    | Ok updated ->
        Assert.Equal(fp @"C:\start\docs", updated.Right.Path)
        Assert.Equal(fp @"C:\start", updated.Left.Path)
    | Error err -> failwith $"unexpected error %A{err}"
