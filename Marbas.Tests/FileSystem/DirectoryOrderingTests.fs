module Marbas.Tests.FileSystem.DirectoryOrderingTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Tests

let private fp = FileSystemPath.create

let private entry name kind =
    { Name = EntryName.create name
      Kind = kind
      Size = (match kind with File -> Some 1L | Directory -> None)
      Modified = DateTime(2026, 1, 1) }

let private names entries = entries |> List.map (_.Name >> EntryName.value)

[<Fact>]
let ``directories come before files`` () =
    let unordered = [ entry "a.txt" File; entry "zeta" Directory ]

    let ordered = order unordered

    Assert.Equal<string list>([ "zeta"; "a.txt" ], names ordered)

[<Fact>]
let ``names are sorted alphabetically ignoring case`` () =
    let unordered =
        [ entry "delta.txt" File
          entry "Alpha.txt" File
          entry "zulu" Directory
          entry "Bravo" Directory ]

    let ordered = order unordered

    Assert.Equal<string list>([ "Bravo"; "zulu"; "Alpha.txt"; "delta.txt" ], names ordered)

[<Fact>]
let ``non-root listing starts with the parent entry`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\sub"
    fs.AddFile(@"C:\start\sub\a.txt", MockFileData "x")

    let entries = listForDisplay fs (fp @"C:\start\sub") |> Expect.ok

    Assert.Equal("..", EntryName.value entries.Head.Name)
    Assert.Equal(Directory, entries.Head.Kind)

[<Fact>]
let ``root listing has no parent entry`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\a.txt", MockFileData "x")

    let entries = listForDisplay fs (fp @"C:\") |> Expect.ok

    Assert.DoesNotContain(entries, fun e -> EntryName.value e.Name = "..")

[<Fact>]
let ``parent entry is followed by ordered content`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\sub\inner"
    fs.AddFile(@"C:\start\sub\a.txt", MockFileData "x")

    let entries = listForDisplay fs (fp @"C:\start\sub") |> Expect.ok

    Assert.Equal<string list>([ ".."; "inner"; "a.txt" ], names entries)
