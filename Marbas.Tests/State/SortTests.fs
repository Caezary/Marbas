module Marbas.Tests.State.SortTests

open System
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState
open Marbas.Tests

let private fp = FileSystemPath.create

let private en = EntryName.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\big.txt", MockFileData(String.replicate 100 "x"))
    fs.AddFile(@"C:\start\small.md", MockFileData "x")
    fs.AddDirectory @"C:\backup"
    fs

let private loaded side path (fs: IFileSystem) state =
    loadPanel fs side path state |> Expect.ok

let private both (fs: IFileSystem) =
    create (fp @"C:\start")
    |> loaded Left (fp @"C:\start") fs
    |> loaded Right (fp @"C:\backup") fs

let private names entries =
    entries |> List.map (fun (e: Entry) -> EntryName.value e.Name)

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
    let before = cursorEntry state.Left

    let sorted = sortBy SortKey.BySize Left state

    Assert.Equal(Some(en "small.md"), before |> Option.map _.Name)
    Assert.Equal(2, sorted.Left.Cursor)
    Assert.Equal(before |> Option.map _.Name, cursorEntry sorted.Left |> Option.map _.Name)

[<Fact>]
let ``sorting keeps the marks`` () =
    let fs = mockFs ()
    let state = both fs |> setCursor Left 2 |> toggleSelection Left

    let sorted = sortBy SortKey.ByExtension Left state

    Assert.Equal<EntryName list>([ en "big.txt" ], selectedEntries sorted.Left |> List.map _.Name)

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
    fs.AddFile(@"C:\backup\huge.txt", MockFileData(String.replicate 50 "x"))
    fs.AddFile(@"C:\backup\tiny.md", MockFileData "x")

    let state =
        both fs |> sortBy SortKey.BySize Left |> loaded Left (fp @"C:\backup") fs

    Assert.Equal(SortOrder.create SortKey.BySize SortDirection.Ascending, state.Left.Sort)
    Assert.Equal<string list>([ ".."; "tiny.md"; "huge.txt" ], names state.Left.Entries)
