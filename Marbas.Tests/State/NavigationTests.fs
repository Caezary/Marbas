module Marbas.Tests.State.NavigationTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs/inner")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs :> IFileSystem

let private loaded side path fs state =
    loadPanel fs side path state |> Expect.ok

let private names entries =
    entries |> List.map (fun (e: Entry) -> EntryName.value e.Name)

[<Fact>]
let ``cursor is reset to zero after loading a panel`` () =
    let fs = mockFs ()

    let state =
        create (fp (at "start"))
        |> loaded Left (fp (at "start")) fs
        |> setCursor Left 2
        |> loaded Left (fp (at "start/docs")) fs

    Assert.Equal(0, state.Left.Cursor)

[<Fact>]
let ``enter descends into the directory under the cursor`` () =
    let fs = mockFs ()
    let state =
        create (fp (at "start"))
        |> loaded Left (fp (at "start")) fs
        |> setCursor Left 1

    let updated = enter fs state |> Expect.ok

    Assert.Equal(fp (at "start/docs"), updated.Left.Path)
    Assert.Equal<string list>([ ".."; "inner" ], names updated.Left.Entries)
    Assert.Equal(0, updated.Left.Cursor)

[<Fact>]
let ``enter on the parent entry goes up with cursor on the directory left behind`` () =
    let fs = mockFs ()
    let state =
        create (fp (at "start/docs"))
        |> loaded Left (fp (at "start/docs")) fs
        |> setCursor Left 0

    let updated = enter fs state |> Expect.ok

    Assert.Equal(fp (at "start"), updated.Left.Path)
    Assert.Equal<string list>([ ".."; "docs"; "readme.txt" ], names updated.Left.Entries)
    Assert.Equal(1, updated.Left.Cursor)

[<Fact>]
let ``enter on a file leaves the state unchanged`` () =
    let fs = mockFs ()
    let state =
        create (fp (at "start"))
        |> loaded Left (fp (at "start")) fs
        |> setCursor Left 2

    let result = enter fs state

    Assert.Equal(Ok state, result)

[<Fact>]
let ``enter acts on the active panel only`` () =
    let fs = mockFs ()
    let state =
        create (fp (at "start"))
        |> loaded Left (fp (at "start")) fs
        |> loaded Right (fp (at "start")) fs
        |> activate Right
        |> setCursor Right 1

    let updated = enter fs state |> Expect.ok

    Assert.Equal(fp (at "start/docs"), updated.Right.Path)
    Assert.Equal(fp (at "start"), updated.Left.Path)
