module Marbas.Tests.State.SortTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Domain
open Marbas.State
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/big.txt", MockFileData(String.replicate 100 "x"))
    fs.AddFile(at "start/small.md", MockFileData "x")
    fs.AddDirectory(at "backup")
    fs

let private loaded side path (fs: IFileSystem) state =
    loadPanel fs side path state |> Expect.ok

let private both (fs: IFileSystem) =
    create (fp (at "start"))
    |> loaded Left (fp (at "start")) fs
    |> loaded Right (fp (at "backup")) fs

let private names entries =
    entries |> Seq.map (fun (e: Entry) -> EntryName.value e.Name) |> List.ofSeq

[<Fact>]
let ``a panel is sorted by name until another key is chosen`` () =
    let fs = mockFs ()

    let state = both fs

    Assert.Equal(SortOrder.byName, state.Left.Sort)
    Assert.Equal<string list>([ ".."; "docs"; "big.txt"; "small.md" ], names state.Left.Entries)

[<Fact>]
let ``sorting by size reorders the entries of the panel`` () =
    let fs = mockFs ()
    let state = both fs

    let sorted = sortBy SortKey.BySize Left state

    Assert.Equal(SortOrder.create SortKey.BySize SortDirection.Ascending, sorted.Left.Sort)
    Assert.Equal<string list>([ ".."; "docs"; "small.md"; "big.txt" ], names sorted.Left.Entries)

[<Fact>]
let ``choosing the same key again reverses the panel`` () =
    let fs = mockFs ()
    let state = both fs |> sortBy SortKey.BySize Left

    let sorted = sortBy SortKey.BySize Left state

    Assert.Equal(SortOrder.create SortKey.BySize SortDirection.Descending, sorted.Left.Sort)
    Assert.Equal<string list>([ ".."; "docs"; "big.txt"; "small.md" ], names sorted.Left.Entries)

[<Fact>]
let ``the cursor stays on the entry it was on`` () =
    let fs = mockFs ()
    let state = both fs |> setCursor Left 3
    let before = PanelState.cursorEntry state.Left

    let sorted = sortBy SortKey.BySize Left state

    Assert.Equal(Some(en "small.md"), before |> Option.map _.Name)
    Assert.Equal(2, sorted.Left.Cursor)
    Assert.Equal(before |> Option.map _.Name, PanelState.cursorEntry sorted.Left |> Option.map _.Name)

[<Fact>]
let ``sorting keeps the marks`` () =
    let fs = mockFs ()
    let state = both fs |> setCursor Left 2 |> toggleSelection Left

    let sorted = sortBy SortKey.ByExtension Left state

    Assert.Equal<EntryName list>([ en "big.txt" ], PanelState.selectedEntries sorted.Left |> List.map _.Name)

[<Fact>]
let ``sorting one panel leaves the other one alone`` () =
    let fs = mockFs ()
    let state = both fs

    let sorted = sortBy SortKey.BySize Left state

    Assert.Equal(SortOrder.byName, sorted.Right.Sort)

[<Fact>]
let ``a refreshed panel keeps its sort order`` () =
    let fs = mockFs ()
    let state = both fs |> sortBy SortKey.BySize Left

    let refreshed = refresh fs state |> Expect.ok

    Assert.Equal<string list>([ ".."; "docs"; "small.md"; "big.txt" ], names refreshed.Left.Entries)

[<Fact>]
let ``a panel opened on another directory keeps its sort order`` () =
    let fs = mockFs ()
    fs.AddFile(at "backup/huge.txt", MockFileData(String.replicate 50 "x"))
    fs.AddFile(at "backup/tiny.md", MockFileData "x")

    let state =
        both fs |> sortBy SortKey.BySize Left |> loaded Left (fp (at "backup")) fs

    Assert.Equal(SortOrder.create SortKey.BySize SortDirection.Ascending, state.Left.Sort)
    Assert.Equal<string list>([ ".."; "tiny.md"; "huge.txt" ], names state.Left.Entries)
