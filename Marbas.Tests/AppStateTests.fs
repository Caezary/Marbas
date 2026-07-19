module Marbas.Tests.AppStateTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem.DirectoryListing
open Marbas.State
open Marbas.State.AppState

[<Fact>]
let ``initial state points both panels at the start path with left active`` () =
    let state = create @"C:\start"

    Assert.Equal(@"C:\start", state.Left.Path)
    Assert.Equal(@"C:\start", state.Right.Path)
    Assert.Empty state.Left.Entries
    Assert.Empty state.Right.Entries
    Assert.Equal(Left, state.Active)

[<Fact>]
let ``toggling the active panel flips between sides`` () =
    let state = create @"C:\start"

    Assert.Equal(Right, (toggleActive state).Active)
    Assert.Equal(Left, (toggleActive (toggleActive state)).Active)

[<Fact>]
let ``activate selects the given side`` () =
    let state = create @"C:\start" |> activate Right

    Assert.Equal(Right, state.Active)
    Assert.Equal(state.Right, activePanel state)

[<Fact>]
let ``loading a panel fills its entries and leaves the other side untouched`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")

    match create @"C:\start" |> loadPanel fs Right @"C:\start" with
    | Ok state ->
        Assert.Equal<string list>(
            [ ".."; "docs"; "readme.txt" ],
            state.Right.Entries |> List.map (fun e -> e.Name)
        )

        Assert.Empty state.Left.Entries
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``loading a missing directory returns the error and keeps state intact`` () =
    let fs = MockFileSystem()

    Assert.Equal(
        Error(DirectoryNotFound @"C:\nope"),
        create @"C:\start" |> loadPanel fs Left @"C:\nope"
    )
