module Marbas.Tests.FileSystem.ArchiveDirectoryTimeTests

open System
open System.IO
open System.Formats.Tar
open System.Reflection
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open ICSharpCode.SharpZipLib.BZip2
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private inside raw =
    ArchivePath.tryCreate raw |> Expect.some

let private docsMoment = DateTimeOffset(DateTime(2024, 3, 9, 14, 20, 30))

let private deepMoment = DateTimeOffset(DateTime(2023, 11, 5, 8, 10, 12))

let private planted (fs: #MockFileSystem) =
    fs.AddDirectory(at "backup")
    fs.AddDirectory(at "out")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")
    fs.AddFile(at "loose/added.txt", MockFileData "added")
    fs.Directory.SetLastWriteTimeUtc(at "work/docs/deep", deepMoment.UtcDateTime)
    fs.Directory.SetLastWriteTimeUtc(at "work/docs", docsMoment.UtcDateTime)
    fs

let private dated () = planted (MockFileSystem())

let private archiveNamed extension = fp (at $"backup/data.{extension}")

let private formatFor (fs: IFileSystem) path =
    FileSystemPath.name fs path
    |> ArchiveKind.recognise
    |> Option.bind ArchiveFormat.tryCreate
    |> Expect.some

let private packing (fs: IFileSystem) archive sources =
    Archive.write
        fs
        unattended
        { Path = archive
          Format = formatFor fs archive
          Compression = Normal
          Password = None }
        sources
    |> ignore

let private unpacking (fs: IFileSystem) archive targets =
    Archive.extract
        fs
        unattended
        { Archive = archive
          Password = None
          Destination = fp (at "out") }
        targets
    |> Expect.ok

let private storedMoment (fs: IFileSystem) archive (raw: string) =
    let segments = ArchivePath.segments (inside raw)

    ArchiveIndex.read fs archive
    |> Expect.ok
    |> List.find (fun info -> ArchiveEntryInfo.isDirectory info && info.Segments = segments)
    |> _.Modified

let private momentOf (fs: IFileSystem) path =
    DateTimeOffset(fs.Directory.GetLastWriteTimeUtc(path: string), TimeSpan.Zero)

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a directory packed keeps its time`` extension =
    let fs = dated ()
    let archive = archiveNamed extension

    packing fs archive [ fp (at "work/docs") ]

    Assert.Equal(docsMoment, storedMoment fs archive "docs")

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a directory below the one packed keeps its own time`` extension =
    let fs = dated ()
    let archive = archiveNamed extension

    packing fs archive [ fp (at "work/docs") ]

    Assert.Equal(deepMoment, storedMoment fs archive "docs/deep")

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a directory added to an archive keeps its time`` extension =
    let fs = dated ()
    let archive = archiveNamed extension
    packing fs archive [ fp (at "loose/added.txt") ]

    Archive.addEntries fs unattended archive ArchivePath.root [ fp (at "work/docs") ]
    |> ignore

    Assert.Equal(docsMoment, storedMoment fs archive "docs")
    Assert.Equal(deepMoment, storedMoment fs archive "docs/deep")

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a directory keeps its time when something else joins the archive`` extension =
    let fs = dated ()
    let archive = archiveNamed extension
    packing fs archive [ fp (at "work/docs") ]

    Archive.addEntries fs unattended archive ArchivePath.root [ fp (at "loose/added.txt") ]
    |> ignore

    Assert.Equal(docsMoment, storedMoment fs archive "docs")
    Assert.Equal(deepMoment, storedMoment fs archive "docs/deep")

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a directory unpacked takes the time its entry carries`` extension =
    let fs = dated ()
    let archive = archiveNamed extension
    packing fs archive [ fp (at "work/docs") ]

    unpacking fs archive [ inside "docs" ] |> ignore

    Assert.Equal(docsMoment, momentOf fs (at "out/docs"))

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a directory below the one unpacked takes its own time`` extension =
    let fs = dated ()
    let archive = archiveNamed extension
    packing fs archive [ fp (at "work/docs") ]

    unpacking fs archive [ inside "docs" ] |> ignore

    Assert.Equal(deepMoment, momentOf fs (at "out/docs/deep"))

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a directory unpacked as the target itself takes its time`` extension =
    let fs = dated ()
    let archive = archiveNamed extension
    packing fs archive [ fp (at "work/docs") ]

    unpacking fs archive [ inside "docs/deep" ] |> ignore

    Assert.Equal(deepMoment, momentOf fs (at "out/deep"))

let private compressing extension (file: Stream) =
    match extension with
    | "tar.bz2" -> new BZip2OutputStream(file) :> Stream
    | _ -> file

let private tarred (fs: IFileSystem) extension =
    let archive = archiveNamed extension
    use file = fs.File.Create(FileSystemPath.value archive)
    use compressed = compressing extension file
    use tar = new TarWriter(compressed, TarEntryFormat.Pax, true)

    tar.WriteEntry(PaxTarEntry(TarEntryType.Directory, "docs/", ModificationTime = docsMoment))

    use inner = fs.File.OpenRead(at "work/docs/inner.txt")
    tar.WriteEntry(PaxTarEntry(TarEntryType.RegularFile, "docs/inner.txt", DataStream = inner))
    archive

[<Theory>]
[<InlineData("tar")>]
[<InlineData("tar.bz2")>]
let ``a directory of a tar another tool wrote is unpacked at its time`` extension =
    let fs = dated ()
    let archive = tarred fs extension

    unpacking fs archive [ inside "docs" ] |> ignore

    Assert.Equal(docsMoment, momentOf fs (at "out/docs"))

let private embedded name =
    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name: string)
    use buffer = new MemoryStream()
    stream.CopyTo buffer
    buffer.ToArray()

[<Fact>]
let ``a directory of a tar xz is unpacked at the time its entry carries`` () =
    let fs = dated ()
    let archive = archiveNamed "tar.xz"
    fs.AddFile(FileSystemPath.value archive, MockFileData(embedded "sample.tar.xz"))
    let stored = storedMoment fs archive "docs"

    unpacking fs archive [ inside "docs" ] |> ignore

    Assert.NotEqual(DateTimeOffset.MinValue, stored)
    Assert.Equal(stored, momentOf fs (at "out/docs"))

[<Fact>]
let ``a directory entry that carries no time leaves the directory as the system made it`` () =
    let fs = dated ()
    let archive = archiveNamed "7z"
    packing fs archive [ fp (at "loose/added.txt") ]

    Archive.makeEntryDirectory fs archive ArchivePath.root (en "fresh") |> Expect.ok

    let started = DateTimeOffset.UtcNow.AddMinutes -1.0

    unpacking fs archive [ inside "fresh" ] |> ignore

    Assert.Equal(DateTimeOffset.MinValue, storedMoment fs archive "fresh")
    Assert.InRange(momentOf fs (at "out/fresh"), started, DateTimeOffset.MaxValue)

[<Fact>]
let ``a directory made for a file alone, with no entry of its own, is left as the system made it`` () =
    let fs = dated ()
    let archive = archiveNamed "zip"
    packing fs archive [ fp (at "work/docs") ]
    let started = DateTimeOffset.UtcNow.AddMinutes -1.0

    unpacking fs archive [ inside "docs/deep/leaf.txt" ] |> ignore

    Assert.InRange(momentOf fs (at "out"), started, DateTimeOffset.MaxValue)

[<Fact>]
let ``a directory whose time cannot be set fails its target and leaves the others unpacked`` () =
    let fs = planted (Fakes.UndatableDirectory(at "out/docs"))
    let archive = archiveNamed "zip"
    packing fs archive [ fp (at "work/docs"); fp (at "work/notes.txt") ]

    let results = unpacking fs archive [ inside "docs"; inside "notes.txt" ]

    Assert.Equal<Result<unit, OperationError> list>(
        [ Error(AccessDenied(fp (at "out/docs"))); Ok() ],
        results |> List.map _.Outcome
    )

    Assert.Equal("inner", fs.File.ReadAllText(at "out/docs/inner.txt"))
