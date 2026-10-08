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
    let visibility = EntryVisibility.ofName (en "notes.txt")

    Assert.Equal(Plain, visibility)

[<Fact>]
let ``a leading dot hides a name`` () =
    let visibility = EntryVisibility.ofName (en ".gitignore")

    Assert.Equal(Hidden, visibility)

[<Fact>]
let ``a leading dot hides an entry the system left plain`` () =
    let visibility = EntryVisibility.classify (en ".gitignore") Plain

    Assert.Equal(Hidden, visibility)

[<Fact>]
let ``an entry the system marked hidden is hidden whatever its name`` () =
    let visibility = EntryVisibility.classify (en "desktop.ini") Hidden

    Assert.Equal(Hidden, visibility)

[<Fact>]
let ``an ordinary name the system left plain is plain`` () =
    let visibility = EntryVisibility.classify (en "notes.txt") Plain

    Assert.Equal(Plain, visibility)

[<Fact>]
let ``toggling a concealing panel shows the hidden entries`` () =
    let hidden = HiddenEntries.toggle Concealed

    Assert.Equal(Shown, hidden)

[<Fact>]
let ``toggling a showing panel conceals the hidden entries`` () =
    let hidden = HiddenEntries.toggle Shown

    Assert.Equal(Concealed, hidden)

[<Fact>]
let ``concealing admits a plain entry`` () =
    let admitted = HiddenEntries.admits Concealed Plain

    Assert.True admitted

[<Fact>]
let ``concealing turns a hidden entry away`` () =
    let admitted = HiddenEntries.admits Concealed Hidden

    Assert.False admitted

[<Fact>]
let ``showing admits a plain entry`` () =
    let admitted = HiddenEntries.admits Shown Plain

    Assert.True admitted

[<Fact>]
let ``showing admits a hidden entry`` () =
    let admitted = HiddenEntries.admits Shown Hidden

    Assert.True admitted

[<Fact>]
let ``reading a directory says which entries are hidden`` () =
    let fs = mockFs ()

    let entries = read fs (fp (at "start")) |> Expect.ok

    let byName name =
        entries |> List.find (fun e -> e.Name = en name)

    Assert.Equal(Plain, (byName "notes.txt").Visibility)
    Assert.Equal(Plain, (byName "docs").Visibility)
    Assert.Equal(Hidden, (byName ".config").Visibility)
    Assert.Equal(Hidden, (byName ".git").Visibility)
    Assert.Equal(Hidden, (byName "swap.sys").Visibility)
    Assert.Equal(Hidden, (byName "cache").Visibility)

[<Fact>]
let ``a concealing listing leaves the hidden entries out`` () =
    let fs = mockFs ()

    let entries =
        listForDisplay fs SortOrder.byName Concealed (fp (at "start")) |> Expect.ok

    Assert.Equal<string list>([ ".."; "docs"; "notes.txt" ], names entries)

[<Fact>]
let ``a showing listing lists the hidden entries in their usual place`` () =
    let fs = mockFs ()

    let entries =
        listForDisplay fs SortOrder.byName Shown (fp (at "start")) |> Expect.ok

    Assert.Equal<string list>([ ".."; ".git"; "cache"; "docs"; ".config"; "notes.txt"; "swap.sys" ], names entries)

[<Fact>]
let ``the parent entry is never concealed`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/sub")
    markDirectory fs (at "start")

    let entries =
        listForDisplay fs SortOrder.byName Concealed (fp (at "start/sub")) |> Expect.ok

    Assert.Equal<string list>([ ".." ], names entries)
