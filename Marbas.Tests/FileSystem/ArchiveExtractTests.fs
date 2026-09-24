module Marbas.Tests.FileSystem.ArchiveExtractTests

open System
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private zip = ArchiveFormat.tryCreate Zip |> Expect.some

let private tarGz = ArchiveFormat.tryCreate TarGz |> Expect.some

let private archive = at "backup/data.zip"

let private tarArchive = at "backup/data.tar.gz"

let private destination = at "out"

let private inside raw = ArchivePath.tryCreate raw |> Expect.some

let private packing format path password =
    { Path = fp path
      Format = format
      Compression = Normal
      Password = password }

let private unpacking path password =
    { Archive = fp path
      Password = password
      Destination = fp destination }

let private sources = [ fp (at "work/notes.txt"); fp (at "work/docs") ]

let private tree () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddDirectory(at "out")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")
    fs.AddDirectory(at "work/docs/empty")
    fs

let private packed password =
    let fs = tree ()
    Archive.write fs unattended (packing zip archive password) sources |> ignore
    fs

let private keyedByHand (fs: IFileSystem) path (keys: string list) =
    use stream = fs.File.Create path
    use written = new IO.Compression.ZipArchive(stream, IO.Compression.ZipArchiveMode.Create, true)

    keys |> List.iter (written.CreateEntry >> ignore)

let private extracted (fs: IFileSystem) control request targets =
    Archive.extract fs control request targets |> Expect.ok

let private watching cancelAfter =
    let reported = ResizeArray<FileSystemPath>()

    let control =
        { Report = reported.Add
          IsCancelled = fun () -> reported.Count >= cancelAfter }

    reported, control

let private damagedPath error =
    match error with
    | ArchiveDamaged(path, _) -> Some path
    | _ -> None

let private itemNames (fs: IFileSystem) (results: ItemResult list) =
    results |> List.map (fun item -> EntryName.value (ItemSource.name fs item.Source))

let private outcomes (fs: IFileSystem) (results: ItemResult list) =
    results |> List.map (fun result -> EntryName.value (ItemSource.name fs result.Source), result.Outcome)

[<Fact>]
let ``a file target is written under the destination`` () =
    let fs = packed None

    extracted fs unattended (unpacking archive None) [ inside "notes.txt" ] |> ignore

    Assert.Equal("notes", fs.File.ReadAllText(at "out/notes.txt"))

[<Fact>]
let ``a directory target takes the tree below it`` () =
    let fs = packed None

    extracted fs unattended (unpacking archive None) [ inside "docs" ] |> ignore

    Assert.Equal("inner", fs.File.ReadAllText(at "out/docs/inner.txt"))
    Assert.Equal("leaf", fs.File.ReadAllText(at "out/docs/deep/leaf.txt"))
    Assert.True(fs.Directory.Exists(at "out/docs/empty"))

[<Fact>]
let ``a target deeper in the archive is written under its own name`` () =
    let fs = packed None

    extracted fs unattended (unpacking archive None) [ inside "docs/deep" ] |> ignore

    Assert.Equal("leaf", fs.File.ReadAllText(at "out/deep/leaf.txt"))
    Assert.False(fs.Directory.Exists(at "out/docs"))

[<Fact>]
let ``what was packed comes back as the tree it started from`` () =
    let fs = packed None

    extracted fs unattended (unpacking archive None) [ inside "notes.txt"; inside "docs" ]
    |> ignore

    Assert.Equal<string list>(
        [ at "out/docs"; at "out/notes.txt" ],
        fs.Directory.EnumerateFileSystemEntries(destination) |> List.ofSeq |> List.sort
    )

    Assert.Equal<string list>(
        [ at "out/docs/deep"; at "out/docs/empty"; at "out/docs/inner.txt" ],
        fs.Directory.EnumerateFileSystemEntries(at "out/docs") |> List.ofSeq |> List.sort
    )

[<Fact>]
let ``the root as a target empties the whole archive into the destination`` () =
    let fs = packed None

    let results = extracted fs unattended (unpacking archive None) [ ArchivePath.root ]

    Assert.Equal<string list>([ "data.zip" ], itemNames fs results)
    Assert.Equal("notes", fs.File.ReadAllText(at "out/notes.txt"))
    Assert.Equal("inner", fs.File.ReadAllText(at "out/docs/inner.txt"))

[<Fact>]
let ``the modification time of a file is restored`` () =
    let fs = tree ()
    let modified = DateTime(2026, 7, 1, 12, 30, 0)
    let file = MockFileData "dated"
    file.LastWriteTime <- DateTimeOffset modified
    fs.AddFile(at "work/dated.txt", file)
    Archive.write fs unattended (packing zip archive None) [ fp (at "work/dated.txt") ] |> ignore

    extracted fs unattended (unpacking archive None) [ inside "dated.txt" ] |> ignore

    Assert.Equal(modified, fs.FileInfo.New(at "out/dated.txt").LastWriteTime)

[<Fact>]
let ``every target answers for itself`` () =
    let fs = packed None

    let results = extracted fs unattended (unpacking archive None) [ inside "notes.txt"; inside "docs" ]

    Assert.Equal<(string * Result<unit, OperationError>) list>(
        [ "notes.txt", Ok(); "docs", Ok() ],
        outcomes fs results
    )

[<Fact>]
let ``every entry is reported as it is written`` () =
    let fs = packed None
    let reported, control = watching Int32.MaxValue

    extracted fs control (unpacking archive None) [ inside "docs" ] |> ignore

    Assert.Equal<string list>(
        [ at "out/docs/deep/leaf.txt"; at "out/docs/inner.txt" ],
        reported |> Seq.map FileSystemPath.value |> List.ofSeq |> List.sort
    )

[<Fact>]
let ``a cancelled run stops where it stood and keeps what it wrote`` () =
    let fs = packed None
    let _, control = watching 1

    let results = extracted fs control (unpacking archive None) [ inside "notes.txt"; inside "docs" ]

    Assert.Equal<string list>([ "notes.txt" ], itemNames fs results)
    Assert.Equal("notes", fs.File.ReadAllText(at "out/notes.txt"))
    Assert.False(fs.Directory.Exists(at "out/docs"))

[<Fact>]
let ``a target the archive does not hold is not found`` () =
    let fs = packed None

    let results = extracted fs unattended (unpacking archive None) [ inside "missing.txt" ]

    Assert.Equal<(string * Result<unit, OperationError>) list>(
        [ "missing.txt", Error(SourceNotFound(fp (at "backup/data.zip/missing.txt"))) ],
        outcomes fs results
    )

[<Fact>]
let ``an entry that would climb out of the destination is refused`` () =
    let fs = tree ()
    keyedByHand fs archive [ "docs/inner.txt"; "docs/../../evil.txt" ]

    let results = extracted fs unattended (unpacking archive None) [ inside "docs" ]

    Assert.Equal<(string * Result<unit, OperationError>) list>(
        [ "docs", Error(EntryOutsideTarget(fp (at "evil.txt"))) ],
        outcomes fs results
    )

    Assert.False(fs.File.Exists(at "evil.txt"))

[<Fact>]
let ``a protected archive says a password is needed`` () =
    let fs = packed (ArchivePassword.tryCreate "letmein")

    let error = Archive.extract fs unattended (unpacking archive None) [ inside "notes.txt" ] |> Expect.error

    Assert.Equal(PasswordNeeded(fp archive), error)

[<Fact>]
let ``a protected archive gives up its content to the password`` () =
    let fs = packed (ArchivePassword.tryCreate "letmein")
    let request = unpacking archive (ArchivePassword.tryCreate "letmein")

    extracted fs unattended request [ inside "notes.txt" ] |> ignore

    Assert.Equal("notes", fs.File.ReadAllText(at "out/notes.txt"))

[<Fact>]
let ``a tar gz is unpacked the same way`` () =
    let fs = tree ()
    Archive.write fs unattended (packing tarGz tarArchive None) sources |> ignore

    extracted fs unattended (unpacking tarArchive None) [ inside "docs" ] |> ignore

    Assert.Equal("inner", fs.File.ReadAllText(at "out/docs/inner.txt"))
    Assert.Equal("leaf", fs.File.ReadAllText(at "out/docs/deep/leaf.txt"))

[<Fact>]
let ``a name that is no archive is refused before anything is written`` () =
    let fs = tree ()
    fs.AddFile(at "backup/data.bin", MockFileData "whatever")

    let error =
        Archive.extract fs unattended (unpacking (at "backup/data.bin") None) [ inside "docs" ]
        |> Expect.error

    Assert.Equal(KindCannotBeRead(fp (at "backup/data.bin")), error)

[<Fact>]
let ``a damaged archive is refused as a whole`` () =
    let fs = tree ()
    fs.AddFile(archive, MockFileData "not an archive at all")

    let error =
        Archive.extract fs unattended (unpacking archive None) [ inside "docs" ]
        |> Expect.error

    Assert.Equal(Some(fp archive), damagedPath error)

let private plan path password targets =
    { Request = unpacking path password
      Targets = targets }

[<Fact>]
let ``several archives are emptied in one batch`` () =
    let fs = packed None
    Archive.write fs unattended (packing zip (at "backup/other.zip") None) [ fp (at "work/docs") ]
    |> ignore

    let results =
        Archive.extractAll
            fs
            unattended
            [ plan archive None [ inside "notes.txt" ]
              plan (at "backup/other.zip") None [ inside "docs" ] ]

    Assert.Equal<(string * Result<unit, OperationError>) list>(
        [ "notes.txt", Ok(); "docs", Ok() ],
        outcomes fs results
    )

    Assert.Equal("notes", fs.File.ReadAllText(at "out/notes.txt"))
    Assert.Equal("inner", fs.File.ReadAllText(at "out/docs/inner.txt"))

[<Fact>]
let ``an archive that refuses as a whole answers for itself under its own name`` () =
    let fs = tree ()
    fs.AddFile(at "backup/data.bin", MockFileData "whatever")

    let results =
        Archive.extractAll fs unattended [ plan (at "backup/data.bin") None [ ArchivePath.root ] ]

    Assert.Equal<(string * Result<unit, OperationError>) list>(
        [ "data.bin", Error(ArchiveFailed(KindCannotBeRead(fp (at "backup/data.bin")))) ],
        outcomes fs results
    )

[<Fact>]
let ``a protected archive is known to need a password`` () =
    let fs = packed (ArchivePassword.tryCreate "letmein")

    let needed = Archive.needsPassword fs (fp archive)

    Assert.Equal(Ok true, needed)

[<Fact>]
let ``an archive with nothing to hide needs no password`` () =
    let fs = packed None

    let needed = Archive.needsPassword fs (fp archive)

    Assert.Equal(Ok false, needed)

[<Fact>]
let ``a kind that carries no password never needs one`` () =
    let fs = tree ()
    Archive.write fs unattended (packing tarGz tarArchive None) sources |> ignore

    let needed = Archive.needsPassword fs (fp tarArchive)

    Assert.Equal(Ok false, needed)

[<Fact>]
let ``an archive that is gone says so instead of needing no password`` () =
    let fs = tree ()

    let refusal = Archive.needsPassword fs (fp archive) |> Expect.error

    Assert.Equal(ArchiveNotFound(fp archive), refusal)

[<Fact>]
let ``an archive that cannot be opened says so instead of needing no password`` () =
    let fs = tree ()
    fs.AddFile(archive, MockFileData "not an archive at all")

    let refused = Archive.needsPassword fs (fp archive) |> Result.isError

    Assert.True refused
