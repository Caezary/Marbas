module Marbas.Tests.FileSystem.ArchiveAcrossTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private inside raw = ArchivePath.tryCreate raw |> Expect.some

let private source = fp (at "start/from.zip")

let private target = fp (at "start/into.zip")

let private workspace = fp (at "temp/Marbas/session")

let private request archive =
    { Archive = archive
      Password = None
      Destination = workspace }

let private packing path format sources (fs: MockFileSystem) =
    Archive.write
        fs
        Operations.unattended
        { Path = fp path
          Format = ArchiveFormat.ofKind format |> Expect.some
          Compression = Normal
          Password = None }
        sources
    |> ignore

    fs

let private both fromKind intoKind =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddDirectory(at "temp")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")
    fs.AddFile(at "other/kept.txt", MockFileData "kept")

    fs
    |> packing (at "start/from.zip") fromKind [ fp (at "work/notes.txt"); fp (at "work/docs") ]
    |> packing (at "start/into.zip") intoKind [ fp (at "other/kept.txt") ]

let private pair () = both Zip Zip

let private names (fs: IFileSystem) archive level =
    ArchiveListing.read fs archive level
    |> Expect.ok
    |> List.map (_.Name >> EntryName.value)
    |> List.sort

let private outcomes (results: Operations.ItemResult list) =
    results |> List.map (fun item -> EntryName.value item.Name, item.Outcome)

let private stopped () =
    { Operations.unattended with
        IsCancelled = fun () -> true }

[<Fact>]
let ``an entry copied between archives lands in the other and stays in its own`` () =
    let fs = pair ()

    let results =
        Archive.copyAcross fs Operations.unattended (request source) [ inside "notes.txt" ] target ArchivePath.root
        |> Expect.ok

    Assert.Equal<string list>([ "kept.txt"; "notes.txt" ], names fs target ArchivePath.root)
    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs source ArchivePath.root)
    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>([ "notes.txt", Ok() ], outcomes results)

[<Fact>]
let ``a directory copied between archives brings the tree below it`` () =
    let fs = pair ()

    Archive.copyAcross fs Operations.unattended (request source) [ inside "docs" ] target ArchivePath.root
    |> Expect.ok
    |> ignore

    Assert.Equal<string list>([ "docs"; "kept.txt" ], names fs target ArchivePath.root)
    Assert.Equal<string list>([ "deep"; "inner.txt" ], names fs target (inside "docs"))
    Assert.Equal<string list>([ "leaf.txt" ], names fs target (inside "docs/deep"))

[<Fact>]
let ``an entry copied into a level below the top lands there`` () =
    let fs = both Zip Zip
    Archive.makeEntryDirectory fs target ArchivePath.root (EntryName.create "held") |> Expect.ok

    Archive.copyAcross fs Operations.unattended (request source) [ inside "notes.txt" ] target (inside "held")
    |> Expect.ok
    |> ignore

    Assert.Equal<string list>([ "notes.txt" ], names fs target (inside "held"))

[<Theory>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``an entry crosses between kinds that are not the same`` extension =
    let intoKind = ArchiveKind.recognise (EntryName.create $"into.{extension}") |> Expect.some
    let into = fp (at $"start/into.{extension}")

    let fs =
        MockFileSystem()
        |> fun fs ->
            fs.AddDirectory(at "start")
            fs.AddDirectory(at "temp")
            fs.AddFile(at "work/notes.txt", MockFileData "notes")
            fs.AddFile(at "other/kept.txt", MockFileData "kept")
            fs

    packing (at "start/from.zip") Zip [ fp (at "work/notes.txt") ] fs |> ignore
    packing (at $"start/into.{extension}") intoKind [ fp (at "other/kept.txt") ] fs |> ignore

    Archive.copyAcross fs Operations.unattended (request source) [ inside "notes.txt" ] into ArchivePath.root
    |> Expect.ok
    |> ignore

    Assert.Equal<string list>([ "kept.txt"; "notes.txt" ], names fs into ArchivePath.root)

[<Fact>]
let ``an entry moved between archives leaves the archive it came from`` () =
    let fs = pair ()

    let results =
        Archive.moveAcross fs Operations.unattended (request source) [ inside "notes.txt" ] target ArchivePath.root
        |> Expect.ok

    Assert.Equal<string list>([ "kept.txt"; "notes.txt" ], names fs target ArchivePath.root)
    Assert.Equal<string list>([ "docs" ], names fs source ArchivePath.root)
    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>([ "notes.txt", Ok() ], outcomes results)

[<Fact>]
let ``an entry the source archive does not hold is reported and moves nothing`` () =
    let fs = pair ()

    let results =
        Archive.moveAcross fs Operations.unattended (request source) [ inside "gone.txt" ] target ArchivePath.root
        |> Expect.ok

    Assert.Equal<string list>([ "kept.txt" ], names fs target ArchivePath.root)
    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs source ArchivePath.root)

    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>(
        [ "gone.txt", Error(Operations.SourceNotFound(fp (at "start/from.zip/gone.txt"))) ],
        outcomes results
    )

[<Fact>]
let ``a target archive that cannot be changed refuses the whole crossing`` () =
    let fs = pair ()
    fs.AddFile(at "start/into.rar", MockFileData "not a rar at all")
    let unchangeable = fp (at "start/into.rar")

    let refusal =
        Archive.copyAcross fs Operations.unattended (request source) [ inside "notes.txt" ] unchangeable ArchivePath.root
        |> Expect.error

    Assert.Equal(KindCannotBeChanged unchangeable, refusal)
    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs source ArchivePath.root)

[<Fact>]
let ``a crossing leaves the workspace empty`` () =
    let fs = pair ()

    Archive.copyAcross fs Operations.unattended (request source) [ inside "docs" ] target ArchivePath.root
    |> Expect.ok
    |> ignore

    Assert.Empty(fs.Directory.EnumerateFileSystemEntries(at "temp/Marbas/session"))

[<Fact>]
let ``a cancelled crossing leaves both archives as they were`` () =
    let fs = pair ()
    let before = fs.File.ReadAllBytes(at "start/into.zip")

    Archive.moveAcross fs (stopped ()) (request source) [ inside "notes.txt" ] target ArchivePath.root
    |> Expect.ok
    |> ignore

    Assert.Equal<byte array>(before, fs.File.ReadAllBytes(at "start/into.zip"))
    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs source ArchivePath.root)
