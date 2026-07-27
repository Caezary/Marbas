module Marbas.Tests.AppStateTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State
open Marbas.State.AppState

let private fp = FileSystemPath.create

[<Fact>]
let ``initial state points both panels at the start path with left active`` () =
    let state = create (fp @"C:\start")

    Assert.Equal(fp @"C:\start", state.Left.Path)
    Assert.Equal(fp @"C:\start", state.Right.Path)
    Assert.Empty state.Left.Entries
    Assert.Empty state.Right.Entries
    Assert.Equal(Left, state.Active)

[<Fact>]
let ``toggling the active panel flips between sides`` () =
    let state = create (fp @"C:\start")

    Assert.Equal(Right, (toggleActive state).Active)
    Assert.Equal(Left, (toggleActive (toggleActive state)).Active)

[<Fact>]
let ``activate selects the given side`` () =
    let state = create (fp @"C:\start") |> activate Right

    Assert.Equal(Right, state.Active)
    Assert.Equal(state.Right, activePanel state)

[<Fact>]
let ``loading a panel fills its entries and leaves the other side untouched`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")

    match create (fp @"C:\start") |> loadPanel fs Right (fp @"C:\start") with
    | Ok state ->
        Assert.Equal<string list>(
            [ ".."; "docs"; "readme.txt" ],
            state.Right.Entries |> List.map (fun e -> e.Name)
        )

        Assert.Empty state.Left.Entries
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``the cursor entry is the one at the cursor index`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")

    match create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") with
    | Ok state ->
        Assert.Equal("..", (cursorEntry state.Left |> Option.get).Name)

        Assert.Equal(
            "readme.txt",
            (state |> setCursor Left 2 |> activePanel |> cursorEntry |> Option.get).Name)
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``an empty panel has no cursor entry`` () =
    Assert.Equal(None, cursorEntry (create (fp @"C:\start")).Left)

[<Fact>]
let ``loading a missing directory returns the error and keeps state intact`` () =
    let fs = MockFileSystem()

    Assert.Equal(
        Error(DirectoryNotFound(fp @"C:\nope")),
        create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\nope")
    )
