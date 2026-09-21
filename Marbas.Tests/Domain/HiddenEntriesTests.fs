module Marbas.Tests.Domain.HiddenEntriesTests

open System.IO
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private names (entries: Entry seq) =
    entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq

let private markedFile attributes =
    let data = MockFileData "x"
    data.Attributes <- attributes
    data

let private markDirectory (fs: MockFileSystem) path =
    let info = fs.DirectoryInfo.New(path: string)
    info.Attributes <- info.Attributes ||| FileAttributes.Hidden

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddDirectory(at "start/cache")
    markDirectory fs (at "start/cache")
    fs.AddDirectory(at "start/.git")
    fs.AddFile(at "start/notes.txt", MockFileData "x")
    fs.AddFile(at "start/.config", MockFileData "x")
    fs.AddFile(at "start/swap.sys", markedFile FileAttributes.System)
    fs

[<Fact>]
let ``an ordinary name is plain`` () =
    let visibility = EntryVisibility.classify (en "notes.txt") FileAttributes.Normal

    Assert.Equal(Plain, visibility)

[<Fact>]
let ``a leading dot hides an entry`` () =
    let visibility = EntryVisibility.classify (en ".gitignore") FileAttributes.Normal

    Assert.Equal(Hidden, visibility)

[<Fact>]
let ``the hidden attribute hides an entry`` () =
    let visibility = EntryVisibility.classify (en "desktop.ini") FileAttributes.Hidden

    Assert.Equal(Hidden, visibility)

[<Fact>]
let ``the system attribute hides an entry`` () =
    let visibility = EntryVisibility.classify (en "swap.sys") FileAttributes.System

    Assert.Equal(Hidden, visibility)

[<Fact>]
let ``toggling switches between showing and concealing`` () =
    Assert.Equal(Shown, HiddenEntries.toggle Concealed)
    Assert.Equal(Concealed, HiddenEntries.toggle Shown)

[<Fact>]
let ``concealing admits plain entries only`` () =
    Assert.True(HiddenEntries.admits Concealed Plain)
    Assert.False(HiddenEntries.admits Concealed Hidden)

[<Fact>]
let ``showing admits every entry`` () =
    Assert.True(HiddenEntries.admits Shown Plain)
    Assert.True(HiddenEntries.admits Shown Hidden)

[<Fact>]
let ``reading a directory says which entries are hidden`` () =
    let fs = mockFs ()

    let entries = read fs (fp (at "start")) |> Expect.ok
    let byName name = entries |> List.find (fun e -> e.Name = en name)

    Assert.Equal(Plain, (byName "notes.txt").Visibility)
    Assert.Equal(Plain, (byName "docs").Visibility)
    Assert.Equal(Hidden, (byName ".config").Visibility)
    Assert.Equal(Hidden, (byName ".git").Visibility)
    Assert.Equal(Hidden, (byName "swap.sys").Visibility)
    Assert.Equal(Hidden, (byName "cache").Visibility)

[<Fact>]
let ``a concealing listing leaves the hidden entries out`` () =
    let fs = mockFs ()

    let entries = listForDisplay fs SortOrder.byName Concealed (fp (at "start")) |> Expect.ok

    Assert.Equal<string list>([ ".."; "docs"; "notes.txt" ], names entries)

[<Fact>]
let ``a showing listing lists the hidden entries in their usual place`` () =
    let fs = mockFs ()

    let entries = listForDisplay fs SortOrder.byName Shown (fp (at "start")) |> Expect.ok

    Assert.Equal<string list>(
        [ ".."; ".git"; "cache"; "docs"; ".config"; "notes.txt"; "swap.sys" ],
        names entries
    )

[<Fact>]
let ``the parent entry is never concealed`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/sub")
    markDirectory fs (at "start")

    let entries = listForDisplay fs SortOrder.byName Concealed (fp (at "start/sub")) |> Expect.ok

    Assert.Equal<string list>([ ".." ], names entries)
