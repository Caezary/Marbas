module Marbas.Tests.State.HiddenTests

open System.IO
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

let private hiddenFile content =
    let data = MockFileData(content: string)
    data.Attributes <- FileAttributes.Hidden
    data

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "x")
    fs.AddFile(at "start/.config", MockFileData "x")
    fs.AddFile(at "start/vault.dat", hiddenFile "x")
    fs.AddDirectory(at "backup")
    fs.AddFile(at "backup/.keep", MockFileData "x")
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
let ``a panel conceals the hidden entries until it is told otherwise`` () =
    let fs = mockFs ()

    let state = both fs

    Assert.Equal(Concealed, state.Left.Hidden)
    Assert.Equal<string list>([ ".."; "docs"; "notes.txt" ], names state.Left.Entries)

[<Fact>]
let ``toggling brings the hidden entries into the panel`` () =
    let fs = mockFs ()

    let toggled = toggleHidden fs Left (both fs) |> Expect.ok

    Assert.Equal(Shown, toggled.Left.Hidden)
    Assert.Equal<string list>([ ".."; "docs"; ".config"; "notes.txt"; "vault.dat" ], names toggled.Left.Entries)

[<Fact>]
let ``toggling again takes them back out`` () =
    let fs = mockFs ()
    let state = toggleHidden fs Left (both fs) |> Expect.ok

    let toggled = toggleHidden fs Left state |> Expect.ok

    Assert.Equal(Concealed, toggled.Left.Hidden)
    Assert.Equal<string list>([ ".."; "docs"; "notes.txt" ], names toggled.Left.Entries)

[<Fact>]
let ``the cursor stays on the entry it was on`` () =
    let fs = mockFs ()
    let state = both fs |> setCursor Left 2

    let toggled = toggleHidden fs Left state |> Expect.ok

    Assert.Equal(Some(en "notes.txt"), PanelState.cursorEntry toggled.Left |> Option.map _.Name)
    Assert.Equal(3, toggled.Left.Cursor)

[<Fact>]
let ``the marks of the entries that stay survive`` () =
    let fs = mockFs ()
    let state = both fs |> setCursor Left 2 |> toggleSelection Left

    let toggled = toggleHidden fs Left state |> Expect.ok

    Assert.Equal<EntryName list>([ en "notes.txt" ], PanelState.selectedEntries toggled.Left |> List.map _.Name)

[<Fact>]
let ``concealing an entry drops its mark`` () =
    let fs = mockFs ()

    let state =
        toggleHidden fs Left (both fs) |> Expect.ok |> setCursor Left 2 |> toggleSelection Left

    let toggled = toggleHidden fs Left state |> Expect.ok

    Assert.Equal<EntryName list>([], PanelState.selectedEntries toggled.Left |> List.map _.Name)

[<Fact>]
let ``toggling one panel leaves the other one alone`` () =
    let fs = mockFs ()

    let toggled = toggleHidden fs Left (both fs) |> Expect.ok

    Assert.Equal(Concealed, toggled.Right.Hidden)
    Assert.Equal<string list>([ ".." ], names toggled.Right.Entries)

[<Fact>]
let ``a panel keeps showing hidden entries in the next directory it opens`` () =
    let fs = mockFs ()

    let state =
        toggleHidden fs Left (both fs) |> Expect.ok |> loaded Left (fp (at "backup")) fs

    Assert.Equal(Shown, state.Left.Hidden)
    Assert.Equal<string list>([ ".."; ".keep" ], names state.Left.Entries)

[<Fact>]
let ``a refreshed panel keeps showing hidden entries`` () =
    let fs = mockFs ()
    let state = toggleHidden fs Left (both fs) |> Expect.ok

    let refreshed = refresh fs state |> Expect.ok

    Assert.Equal<string list>([ ".."; "docs"; ".config"; "notes.txt"; "vault.dat" ], names refreshed.Left.Entries)

[<Fact>]
let ``toggling a directory that vanished reports an error`` () =
    let fs = mockFs ()
    let state = both fs |> loaded Left (fp (at "start/docs")) fs
    fs.Directory.Delete(at "start/docs")

    let result = toggleHidden fs Left state

    Assert.Equal(Error(DirectoryNotFound(fp (at "start/docs"))), result)
