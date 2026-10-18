module Marbas.Tests.Ui.Find.ResultRowTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.FileSystem.DirectoryListing
open Marbas.Find.FileSearch
open Marbas.Ui.Find
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private moment = DateTimeOffset(2026, 9, 18, 14, 30, 0, TimeSpan.Zero)

let private file name size =
    { Name = EntryName.create name
      Kind = File
      Size = Some size
      Measured = None
      Modified = moment
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

let private directory name =
    { file name 0L with
        Kind = Directory
        Size = None }

let private hitIn location entry = { Location = location; Entry = entry }

let private onDisk where = OnDisk(fp (at where))

let private rowsOf hits = ResultRow.following None hits

[<Fact>]
let ``nothing found is no rows at all`` () =
    let rows = rowsOf []

    Assert.Empty rows

[<Fact>]
let ``the hits of one directory stand under one header`` () =
    let rows =
        rowsOf
            [ hitIn (onDisk "start") (file "a.txt" 1L)
              hitIn (onDisk "start") (file "b.txt" 2L) ]

    Assert.Equal<ResultRow.ResultRow list>(
        [ ResultRow.GroupHeader(onDisk "start")
          ResultRow.Hit(hitIn (onDisk "start") (file "a.txt" 1L))
          ResultRow.Hit(hitIn (onDisk "start") (file "b.txt" 2L)) ],
        rows
    )

[<Fact>]
let ``the groups stand in the order the walk reached them`` () =
    let rows =
        rowsOf
            [ hitIn (onDisk "start/docs") (file "a.txt" 1L)
              hitIn (onDisk "start") (file "b.txt" 2L) ]

    let headers =
        rows
        |> List.choose (function
            | ResultRow.GroupHeader location -> Some location
            | _ -> None)

    Assert.Equal<PanelLocation list>([ onDisk "start/docs"; onDisk "start" ], headers)

[<Fact>]
let ``a walk coming back to a directory heads the run it comes back with`` () =
    let rows =
        rowsOf
            [ hitIn (onDisk "start") (file "a.txt" 1L)
              hitIn (onDisk "start/docs") (file "b.txt" 2L)
              hitIn (onDisk "start") (file "c.txt" 3L) ]

    Assert.Equal<ResultRow.ResultRow list>(
        [ ResultRow.GroupHeader(onDisk "start")
          ResultRow.Hit(hitIn (onDisk "start") (file "a.txt" 1L))
          ResultRow.GroupHeader(onDisk "start/docs")
          ResultRow.Hit(hitIn (onDisk "start/docs") (file "b.txt" 2L))
          ResultRow.GroupHeader(onDisk "start")
          ResultRow.Hit(hitIn (onDisk "start") (file "c.txt" 3L)) ],
        rows
    )

[<Fact>]
let ``rows following a run already open carry no header of their own`` () =
    let rows =
        ResultRow.following (Some(onDisk "start")) [ hitIn (onDisk "start") (file "b.txt" 2L) ]

    Assert.Equal<ResultRow.ResultRow list>([ ResultRow.Hit(hitIn (onDisk "start") (file "b.txt" 2L)) ], rows)

[<Fact>]
let ``rows following another run open one of their own`` () =
    let rows =
        ResultRow.following (Some(onDisk "start/docs")) [ hitIn (onDisk "start") (file "b.txt" 2L) ]

    Assert.Equal<ResultRow.ResultRow list>(
        [ ResultRow.GroupHeader(onDisk "start")
          ResultRow.Hit(hitIn (onDisk "start") (file "b.txt" 2L)) ],
        rows
    )

[<Fact>]
let ``a found directory is a row in the group of its parent`` () =
    let rows = rowsOf [ hitIn (onDisk "start") (directory "docs") ]

    Assert.Equal<ResultRow.ResultRow list>(
        [ ResultRow.GroupHeader(onDisk "start")
          ResultRow.Hit(hitIn (onDisk "start") (directory "docs")) ],
        rows
    )

[<Fact>]
let ``a header spells the directory its hits live in`` () =
    let fs = MockFileSystem()

    let line = ResultRow.describe fs 60 (ResultRow.GroupHeader(onDisk "start/docs"))

    Assert.Equal(at "start/docs", line)

[<Fact>]
let ``a header inside an archive spells the level as a path`` () =
    let fs = MockFileSystem()

    let level =
        InArchive(fp (at "start/backup.zip"), ArchivePath.child ArchivePath.root (EntryName.create "docs"))

    let line = ResultRow.describe fs 60 (ResultRow.GroupHeader level)

    Assert.Equal(at "start/backup.zip/docs", line)

[<Fact>]
let ``a hit stands indented under its header, with its size and its date`` () =
    let fs = MockFileSystem()

    let line =
        ResultRow.describe fs 60 (ResultRow.Hit(hitIn (onDisk "start") (file "notes.txt" 1024L)))

    Assert.StartsWith("  notes.txt", line)
    Assert.EndsWith("2026-09-18 14:30", line)
    Assert.Contains(" 1.0 KB ", line)
