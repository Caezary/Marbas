module Marbas.Tests.FileSystem.DirectoryOrderingTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private entry name kind =
    { Name = EntryName.create name
      Kind = kind
      Size =
        (match kind with
         | File -> Some 1L
         | Directory -> None)
      Measured = None
      Modified = DateTimeOffset(DateTime(2026, 1, 1))
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

let private sized name size =
    { entry name File with
        Size = Some size }

let private dated name day =
    { entry name File with
        Modified = DateTimeOffset(DateTime(2026, 1, day)) }

let private measured name size =
    { entry name Directory with
        Measured = Some size }

let private names (entries: Entry seq) =
    entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq

let private byName = SortOrder.byName

let private descending key =
    SortOrder.create key SortDirection.Descending

let private ascending key =
    SortOrder.create key SortDirection.Ascending

[<Fact>]
let ``directories come before files`` () =
    let unordered = [ entry "a.txt" File; entry "zeta" Directory ]

    let ordered = order byName unordered

    Assert.Equal<string list>([ "zeta"; "a.txt" ], names ordered)

[<Fact>]
let ``names are sorted alphabetically ignoring case`` () =
    let unordered =
        [ entry "delta.txt" File
          entry "Alpha.txt" File
          entry "zulu" Directory
          entry "Bravo" Directory ]

    let ordered = order byName unordered

    Assert.Equal<string list>([ "Bravo"; "zulu"; "Alpha.txt"; "delta.txt" ], names ordered)

[<Fact>]
let ``names starting with punctuation come before names starting with a letter`` () =
    let unordered =
        [ entry "beta.txt" File; entry "_notes.txt" File; entry "alpha.txt" File ]

    let ordered = order byName unordered

    Assert.Equal<string list>([ "_notes.txt"; "alpha.txt"; "beta.txt" ], names ordered)

[<Fact>]
let ``extensions starting with punctuation come before extensions starting with a letter`` () =
    let unordered = [ entry "second.bak" File; entry "first._old" File ]

    let ordered = order (ascending SortKey.ByExtension) unordered

    Assert.Equal<string list>([ "first._old"; "second.bak" ], names ordered)

[<Fact>]
let ``a descending sort reverses the entries inside their groups`` () =
    let unordered =
        [ entry "delta.txt" File
          entry "alpha.txt" File
          entry "zulu" Directory
          entry "bravo" Directory ]

    let ordered = order (descending SortKey.ByName) unordered

    Assert.Equal<string list>([ "zulu"; "bravo"; "delta.txt"; "alpha.txt" ], names ordered)

[<Fact>]
let ``directories stay before files whatever the direction`` () =
    let unordered = [ entry "a.txt" File; entry "zeta" Directory ]

    let ordered = order (descending SortKey.ByName) unordered

    Assert.Equal<string list>([ "zeta"; "a.txt" ], names ordered)

[<Fact>]
let ``sorting by extension groups the files by what follows the last dot`` () =
    let unordered =
        [ entry "notes.txt" File; entry "build.fsproj" File; entry "app.fs" File ]

    let ordered = order (ascending SortKey.ByExtension) unordered

    Assert.Equal<string list>([ "app.fs"; "build.fsproj"; "notes.txt" ], names ordered)

[<Fact>]
let ``entries sharing an extension are sorted by name`` () =
    let unordered = [ entry "second.txt" File; entry "first.txt" File ]

    let ordered = order (ascending SortKey.ByExtension) unordered

    Assert.Equal<string list>([ "first.txt"; "second.txt" ], names ordered)

[<Fact>]
let ``sorting by date puts the oldest entry first`` () =
    let unordered =
        [ dated "newest.txt" 30; dated "oldest.txt" 1; dated "middle.txt" 15 ]

    let ordered = order (ascending SortKey.ByDate) unordered

    Assert.Equal<string list>([ "oldest.txt"; "middle.txt"; "newest.txt" ], names ordered)

[<Fact>]
let ``sorting by date descending puts the newest entry first`` () =
    let unordered = [ dated "oldest.txt" 1; dated "newest.txt" 30 ]

    let ordered = order (descending SortKey.ByDate) unordered

    Assert.Equal<string list>([ "newest.txt"; "oldest.txt" ], names ordered)

[<Fact>]
let ``sorting by size puts the smallest entry first`` () =
    let unordered =
        [ sized "big.txt" 4096L; sized "small.txt" 12L; sized "medium.txt" 512L ]

    let ordered = order (ascending SortKey.BySize) unordered

    Assert.Equal<string list>([ "small.txt"; "medium.txt"; "big.txt" ], names ordered)

[<Fact>]
let ``directories are sorted by what was measured`` () =
    let unordered =
        [ measured "middle" (Exact 500L)
          measured "biggest" (Exact 9000L)
          measured "smallest" (Exact 10L) ]

    let ordered = order (ascending SortKey.BySize) unordered

    Assert.Equal<string list>([ "smallest"; "middle"; "biggest" ], names ordered)

[<Fact>]
let ``a directory measured only in part is sorted by what was counted`` () =
    let unordered = [ measured "partly" (AtLeast 9000L); measured "whole" (Exact 500L) ]

    let ordered = order (ascending SortKey.BySize) unordered

    Assert.Equal<string list>([ "whole"; "partly" ], names ordered)

[<Fact>]
let ``a directory nobody measured counts as nothing`` () =
    let unordered = [ measured "counted" (Exact 10L); entry "unknown" Directory ]

    let ordered = order (ascending SortKey.BySize) unordered

    Assert.Equal<string list>([ "unknown"; "counted" ], names ordered)

[<Fact>]
let ``directories nobody measured keep the order of their names`` () =
    let unordered = [ entry "zulu" Directory; entry "alpha" Directory ]

    let ordered = order (ascending SortKey.BySize) unordered

    Assert.Equal<string list>([ "alpha"; "zulu" ], names ordered)

[<Fact>]
let ``the measured directories turn around with the direction`` () =
    let unordered =
        [ measured "middle" (Exact 500L)
          measured "biggest" (Exact 9000L)
          measured "smallest" (Exact 10L) ]

    let ordered = order (descending SortKey.BySize) unordered

    Assert.Equal<string list>([ "biggest"; "middle"; "smallest" ], names ordered)

[<Fact>]
let ``a measured directory still comes before every file`` () =
    let unordered = [ sized "tiny.txt" 1L; measured "huge" (Exact 9000L) ]

    let ordered = order (descending SortKey.BySize) unordered

    Assert.Equal<string list>([ "huge"; "tiny.txt" ], names ordered)

[<Fact>]
let ``entries of the same size are sorted by name`` () =
    let unordered = [ sized "second.txt" 12L; sized "first.txt" 12L ]

    let ordered = order (ascending SortKey.BySize) unordered

    Assert.Equal<string list>([ "first.txt"; "second.txt" ], names ordered)

[<Fact>]
let ``non-root listing starts with the parent entry`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/sub")
    fs.AddFile(at "start/sub/a.txt", MockFileData "x")

    let entries = listForDisplay fs byName Concealed (fp (at "start/sub")) |> Expect.ok

    Assert.Equal("..", EntryName.value entries[0].Name)
    Assert.Equal(Directory, entries[0].Kind)

[<Fact>]
let ``the parent entry stays first whatever the sort`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/sub/inner")
    fs.AddFile(at "start/sub/a.txt", MockFileData "x")

    let entries =
        listForDisplay fs (descending SortKey.ByName) Concealed (fp (at "start/sub"))
        |> Expect.ok

    Assert.Equal<string list>([ ".."; "inner"; "a.txt" ], names entries)

[<Fact>]
let ``root listing has no parent entry`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "a.txt", MockFileData "x")

    let entries = listForDisplay fs byName Concealed (fp (at "")) |> Expect.ok

    Assert.DoesNotContain(entries, fun e -> EntryName.value e.Name = "..")

[<Fact>]
let ``parent entry is followed by ordered content`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/sub/inner")
    fs.AddFile(at "start/sub/a.txt", MockFileData "x")

    let entries = listForDisplay fs byName Concealed (fp (at "start/sub")) |> Expect.ok

    Assert.Equal<string list>([ ".."; "inner"; "a.txt" ], names entries)
