module Marbas.Tests.FileSystem.ArchiveChangeTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private inside raw = ArchivePath.tryCreate raw |> Expect.some

let private archive = fp (at "start/data.zip")

let private request format path password =
    { Path = fp path
      Format = ArchiveFormat.ofKind format |> Expect.some
      Compression = Normal
      Password = password }

let private packedInto format path password =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddDirectory(at "out")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")
    fs.AddFile(at "loose/added.txt", MockFileData "added")
    fs.AddFile(at "loose/tree/kept.txt", MockFileData "kept")

    Archive.write fs Operations.unattended (request format path password) [ fp (at "work/notes.txt"); fp (at "work/docs") ]
    |> ignore

    fs

let private packed () = packedInto Zip (at "start/data.zip") None

let private names (fs: IFileSystem) level =
    ArchiveListing.read fs archive level
    |> Expect.ok
    |> List.map (_.Name >> EntryName.value)
    |> List.sort

let private outcomes (fs: IFileSystem) (results: Operations.ItemResult list) =
    results |> List.map (fun item -> EntryName.value (Operations.ItemSource.name fs item.Source), item.Outcome)

let private stopped () =
    { Operations.unattended with
        IsCancelled = fun () -> true }

let private extracting destination =
    { Archive = archive
      Password = None
      Destination = fp destination }

[<Fact>]
let ``a source added at the top of an archive is in it`` () =
    let fs = packed ()

    let results =
        Archive.addEntries fs Operations.unattended archive ArchivePath.root [ fp (at "loose/added.txt") ]
        |> Expect.ok

    Assert.Equal<string list>([ "added.txt"; "docs"; "notes.txt" ], names fs ArchivePath.root)
    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>([ "added.txt", Ok() ], outcomes fs results)

[<Fact>]
let ``a source added at a level below the top lands there`` () =
    let fs = packed ()

    Archive.addEntries fs Operations.unattended archive (inside "docs") [ fp (at "loose/added.txt") ]
    |> Expect.ok
    |> ignore

    Assert.Equal<string list>([ "added.txt"; "deep"; "inner.txt" ], names fs (inside "docs"))

[<Fact>]
let ``a directory added to an archive brings the tree below it`` () =
    let fs = packed ()

    Archive.addEntries fs Operations.unattended archive ArchivePath.root [ fp (at "loose/tree") ]
    |> Expect.ok
    |> ignore

    Assert.Equal<string list>([ "docs"; "notes.txt"; "tree" ], names fs ArchivePath.root)
    Assert.Equal<string list>([ "kept.txt" ], names fs (inside "tree"))

[<Fact>]
let ``a source added under a name already there replaces that entry`` () =
    let fs = packed ()
    fs.AddFile(at "loose/notes.txt", MockFileData "fresher")

    Archive.addEntries fs Operations.unattended archive ArchivePath.root [ fp (at "loose/notes.txt") ]
    |> Expect.ok
    |> ignore

    use source = FileWindow.openEntry fs None archive (inside "notes.txt") |> Expect.ok

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs ArchivePath.root)
    Assert.Equal("fresher", System.Text.Encoding.ASCII.GetString(source.Read(0L, 7)))

[<Fact>]
let ``a cancelled add leaves the archive as it was and answers for every source`` () =
    let fs = packed ()
    let before = fs.File.ReadAllBytes(at "start/data.zip")

    let results =
        Archive.addEntries fs (stopped ()) archive ArchivePath.root [ fp (at "loose/added.txt") ]
        |> Expect.ok

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs ArchivePath.root)
    Assert.Equal<byte list>(List.ofArray before, List.ofArray (fs.File.ReadAllBytes(at "start/data.zip")))

    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>(
        [ "added.txt", Error(Operations.Cancelled(fp (at "loose/added.txt"))) ],
        outcomes fs results
    )

[<Fact>]
let ``an entry removed from an archive is gone from it`` () =
    let fs = packed ()

    let results =
        Archive.removeEntries fs Operations.unattended archive [ inside "notes.txt" ] |> Expect.ok

    Assert.Equal<string list>([ "docs" ], names fs ArchivePath.root)
    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>([ "notes.txt", Ok() ], outcomes fs results)

[<Fact>]
let ``a directory removed from an archive takes the tree below it`` () =
    let fs = packed ()

    Archive.removeEntries fs Operations.unattended archive [ inside "docs" ] |> Expect.ok |> ignore

    Assert.Equal<string list>([ "notes.txt" ], names fs ArchivePath.root)

[<Fact>]
let ``an entry the archive does not hold cannot be removed`` () =
    let fs = packed ()

    let results =
        Archive.removeEntries fs Operations.unattended archive [ inside "gone.txt" ] |> Expect.ok

    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>(
        [ "gone.txt", Error(Operations.SourceNotFound(fp (at "start/data.zip/gone.txt"))) ],
        outcomes fs results
    )

[<Fact>]
let ``a cancelled removal leaves every entry where it was`` () =
    let fs = packed ()

    let results = Archive.removeEntries fs (stopped ()) archive [ inside "notes.txt" ] |> Expect.ok

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs ArchivePath.root)

    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>(
        [ "notes.txt", Error(Operations.Cancelled(fp (at "start/data.zip/notes.txt"))) ],
        outcomes fs results
    )

[<Fact>]
let ``a directory made in an archive is listed at its level`` () =
    let fs = packed ()

    Archive.makeEntryDirectory fs archive (inside "docs") (en "fresh") |> Expect.ok

    Assert.Equal<string list>([ "deep"; "fresh"; "inner.txt" ], names fs (inside "docs"))

[<Fact>]
let ``a directory made under a name already there is refused`` () =
    let fs = packed ()

    let failure = Archive.makeEntryDirectory fs archive ArchivePath.root (en "docs") |> Expect.error

    Assert.Equal(Operations.TargetExists(fp (at "start/data.zip/docs")), failure)

[<Fact>]
let ``a source moved into an archive is deleted once it is in`` () =
    let fs = packed ()

    let results =
        Archive.moveIn fs Operations.unattended archive ArchivePath.root [ fp (at "loose/added.txt") ]
        |> Expect.ok

    Assert.Equal<string list>([ "added.txt"; "docs"; "notes.txt" ], names fs ArchivePath.root)
    Assert.False(fs.File.Exists(at "loose/added.txt"))
    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>([ "added.txt", Ok() ], outcomes fs results)

[<Fact>]
let ``a source that never arrived is left where it is`` () =
    let fs = packed ()

    let results =
        Archive.moveIn fs Operations.unattended archive ArchivePath.root [ fp (at "loose/gone.txt") ]
        |> Expect.ok

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs ArchivePath.root)

    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>(
        [ "gone.txt", Error(Operations.SourceNotFound(fp (at "loose/gone.txt"))) ],
        outcomes fs results
    )

[<Fact>]
let ``an entry moved out of an archive lands on disk and leaves the archive`` () =
    let fs = packed ()

    let results =
        Archive.moveOut fs Operations.unattended (extracting (at "out")) [ inside "notes.txt" ]
        |> Expect.ok

    Assert.Equal("notes", fs.File.ReadAllText(at "out/notes.txt"))
    Assert.Equal<string list>([ "docs" ], names fs ArchivePath.root)
    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>([ "notes.txt", Ok() ], outcomes fs results)

[<Fact>]
let ``an entry that never arrived on disk stays in the archive`` () =
    let fs = packed ()

    let results =
        Archive.moveOut fs Operations.unattended (extracting (at "out")) [ inside "gone.txt" ]
        |> Expect.ok

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs ArchivePath.root)

    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>(
        [ "gone.txt", Error(Operations.SourceNotFound(fp (at "start/data.zip/gone.txt"))) ],
        outcomes fs results
    )

let private packedTwins () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddDirectory(at "out")
    fs.AddFile(at "work/a/same.txt", MockFileData "a")
    fs.AddFile(at "work/b/same.txt", MockFileData "b")

    let twins = [ fp (at "work/a"); fp (at "work/b") ]

    Archive.write fs Operations.unattended (request Zip (at "start/data.zip") None) twins
    |> ignore

    fs

[<Fact>]
let ``moving out two entries of one name drops only the one that arrived`` () =
    let fs = packedTwins ()

    let results =
        Archive.moveOut fs Operations.unattended (extracting (at "out")) [ inside "a/same.txt"; inside "c/same.txt" ]
        |> Expect.ok

    Assert.Equal<string list>([], names fs (inside "a"))
    Assert.Equal<string list>([ "same.txt" ], names fs (inside "b"))

    Assert.Equal<(Operations.ItemSource * Result<unit, Operations.OperationError>) list>(
        [ Operations.ItemInArchive(archive, inside "a/same.txt"), Ok()
          Operations.ItemInArchive(archive, inside "c/same.txt"),
          Error(Operations.SourceNotFound(fp (at "start/data.zip/c/same.txt"))) ],
        results |> List.map (fun item -> item.Source, item.Outcome)
    )

[<Fact>]
let ``a kind that cannot be changed refuses by name`` () =
    let fs = packed ()
    fs.AddFile(at "start/data.rar", MockFileData "not a rar at all")
    let unchangeable = fp (at "start/data.rar")

    let refusal =
        Archive.addEntries fs Operations.unattended unchangeable ArchivePath.root [ fp (at "loose/added.txt") ]
        |> Expect.error

    Assert.Equal(KindCannotBeChanged unchangeable, refusal)

[<Fact>]
let ``an archive behind a password refuses to be changed`` () =
    let fs = packedInto Zip (at "start/data.zip") (ArchivePassword.tryCreate "opensesame")

    let refusal =
        Archive.addEntries fs Operations.unattended archive ArchivePath.root [ fp (at "loose/added.txt") ]
        |> Expect.error

    Assert.Equal(ProtectedArchiveCannotBeChanged archive, refusal)

[<Fact>]
let ``nothing refuses a plain zip`` () =
    let fs = packed ()

    Assert.Equal(None, Archive.changeRefusal fs archive)

[<Theory>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a working file already beside the archive is left alone`` extension =
    let target = at $"start/data.{extension}"
    let fs = packedInto (ArchiveKind.recognise (en $"data.{extension}") |> Expect.some) target None
    fs.AddFile(target + ".marbas-new", MockFileData "someone else's file")

    Archive.addEntries fs Operations.unattended (fp target) ArchivePath.root [ fp (at "loose/added.txt") ]
    |> Expect.ok
    |> ignore

    Assert.Equal("someone else's file", fs.File.ReadAllText(target + ".marbas-new"))

[<Theory>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a change leaves no working file behind`` extension =
    let target = at $"start/data.{extension}"
    let fs = packedInto (ArchiveKind.recognise (en $"data.{extension}") |> Expect.some) target None

    Archive.addEntries fs Operations.unattended (fp target) ArchivePath.root [ fp (at "loose/added.txt") ]
    |> Expect.ok
    |> ignore

    Assert.Empty(fs.Directory.EnumerateFiles(at "start", "*.marbas-new*"))

[<Theory>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a cancelled change leaves no working file behind`` extension =
    let target = at $"start/data.{extension}"
    let fs = packedInto (ArchiveKind.recognise (en $"data.{extension}") |> Expect.some) target None

    Archive.addEntries fs (stopped ()) (fp target) ArchivePath.root [ fp (at "loose/added.txt") ]
    |> Expect.ok
    |> ignore

    Assert.Empty(fs.Directory.EnumerateFiles(at "start", "*.marbas-new*"))

[<Theory>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a change that cannot be written leaves no working file behind`` extension =
    let target = at $"start/data.{extension}"
    let kind = ArchiveKind.recognise (en $"data.{extension}") |> Expect.some
    let source = packedInto kind target None

    let fs = Fakes.FullDisk(target + ".marbas-new")
    fs.AddFile(target, MockFileData(source.File.ReadAllBytes target))
    fs.AddFile(at "loose/added.txt", MockFileData "added")

    Archive.addEntries fs Operations.unattended (fp target) ArchivePath.root [ fp (at "loose/added.txt") ]
    |> Expect.error
    |> ignore

    Assert.Empty(fs.Directory.EnumerateFiles(at "start", "*.marbas-new*"))
