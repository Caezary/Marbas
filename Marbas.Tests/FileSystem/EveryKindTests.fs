module Marbas.Tests.FileSystem.EveryKindTests

open System.IO
open System.Formats.Tar
open System.Reflection
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open ICSharpCode.SharpZipLib.BZip2
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private inside raw = ArchivePath.tryCreate raw |> Expect.some

let private sources = [ fp (at "work/notes.txt"); fp (at "work/docs") ]

let private tree () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddDirectory(at "out")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "loose/added.txt", MockFileData "added")
    fs

let private formatFor (fs: IFileSystem) path =
    FileSystemPath.name fs path
    |> ArchiveKind.recognise
    |> Option.bind ArchiveFormat.ofKind
    |> Expect.some

let private packed (fs: IFileSystem) path =
    Archive.write
        fs
        Operations.unattended
        { Path = path
          Format = formatFor fs path
          Compression = Normal
          Password = None }
        sources
    |> ignore

let private names (fs: IFileSystem) archive level =
    ArchiveListing.read fs archive level
    |> Expect.ok
    |> List.map (_.Name >> EntryName.value)
    |> List.sort

let private emptied (fs: IFileSystem) archive =
    Archive.extract
        fs
        Operations.unattended
        { Archive = archive
          Password = None
          Destination = fp (at "out") }
        [ inside "notes.txt"; inside "docs" ]
    |> Expect.ok
    |> List.map (fun item -> EntryName.value (Operations.ItemSource.name fs item.Source), item.Outcome)

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a kind that can be created takes a tree and gives it back`` extension =
    let fs = tree ()
    let archive = fp (at $"start/data.{extension}")
    packed fs archive

    let results = emptied fs archive

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs archive ArchivePath.root)
    Assert.Equal<string list>([ "inner.txt" ], names fs archive (inside "docs"))
    Assert.Equal("notes", fs.File.ReadAllText(at "out/notes.txt"))
    Assert.Equal("inner", fs.File.ReadAllText(at "out/docs/inner.txt"))

    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>(
        [ "notes.txt", Ok(); "docs", Ok() ],
        results
    )

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a kind that can be changed takes an entry and gives it up again`` extension =
    let fs = tree ()
    let archive = fp (at $"start/data.{extension}")
    packed fs archive

    Archive.addEntries fs Operations.unattended archive ArchivePath.root [ fp (at "loose/added.txt") ]
    |> Expect.ok
    |> ignore

    let added = names fs archive ArchivePath.root

    Archive.removeEntries fs Operations.unattended archive [ inside "docs"; inside "added.txt" ]
    |> Expect.ok
    |> ignore

    Assert.Equal<string list>([ "added.txt"; "docs"; "notes.txt" ], added)
    Assert.Equal<string list>([ "notes.txt" ], names fs archive ArchivePath.root)

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a kind that can be changed keeps what it already held`` extension =
    let fs = tree ()
    let archive = fp (at $"start/data.{extension}")
    packed fs archive

    Archive.addEntries fs Operations.unattended archive (inside "docs") [ fp (at "loose/added.txt") ]
    |> Expect.ok
    |> ignore

    use source = FileWindow.openEntry fs None archive (inside "docs/inner.txt") |> Expect.ok

    Assert.Equal<string list>([ "added.txt"; "inner.txt" ], names fs archive (inside "docs"))
    Assert.Equal("inner", System.Text.Encoding.ASCII.GetString(source.Read(0L, 5)))

[<Theory>]
[<InlineData("tar.bz2")>]
[<InlineData("tar.xz")>]
[<InlineData("tar")>]
let ``a kind that can be read is no kind to create`` extension =
    let kind = ArchiveKind.recognise (en $"data.{extension}") |> Expect.some

    Assert.True (ArchiveKind.capabilities kind).CanRead
    Assert.False (ArchiveKind.capabilities kind).CanChange

let private tarred (fs: IFileSystem) (write: Stream -> Stream) path =
    use file = fs.File.Create(FileSystemPath.value path)
    use compressed = write file
    use tar = new TarWriter(compressed, TarEntryFormat.Pax, true)

    use notes = fs.File.OpenRead(at "work/notes.txt")
    tar.WriteEntry(PaxTarEntry(TarEntryType.RegularFile, "notes.txt", DataStream = notes))
    tar.WriteEntry(PaxTarEntry(TarEntryType.Directory, "docs/"))

    use inner = fs.File.OpenRead(at "work/docs/inner.txt")
    tar.WriteEntry(PaxTarEntry(TarEntryType.RegularFile, "docs/inner.txt", DataStream = inner))

let private embedded name =
    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name: string)
    use buffer = new MemoryStream()
    stream.CopyTo buffer
    buffer.ToArray()

[<Fact>]
let ``a tar lists and extracts like every other readable kind`` () =
    let fs = tree ()
    let archive = fp (at "start/data.tar")
    tarred fs id archive

    let results = emptied fs archive

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs archive ArchivePath.root)
    Assert.Equal("inner", fs.File.ReadAllText(at "out/docs/inner.txt"))
    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>([ "notes.txt", Ok(); "docs", Ok() ], results)

[<Fact>]
let ``a tar bz2 lists and extracts like every other readable kind`` () =
    let fs = tree ()
    let archive = fp (at "start/data.tar.bz2")
    tarred fs (fun file -> new BZip2OutputStream(file) :> Stream) archive

    let results = emptied fs archive

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs archive ArchivePath.root)
    Assert.Equal("inner", fs.File.ReadAllText(at "out/docs/inner.txt"))
    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>([ "notes.txt", Ok(); "docs", Ok() ], results)

[<Fact>]
let ``a tar xz lists and extracts like every other readable kind`` () =
    let fs = tree ()
    let archive = fp (at "start/data.tar.xz")
    fs.AddFile(at "start/data.tar.xz", MockFileData(embedded "sample.tar.xz"))

    let results = emptied fs archive

    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs archive ArchivePath.root)
    Assert.Equal("notes", fs.File.ReadAllText(at "out/notes.txt"))
    Assert.Equal("inner", fs.File.ReadAllText(at "out/docs/inner.txt"))
    Assert.Equal<(string * Result<unit, Operations.OperationError>) list>([ "notes.txt", Ok(); "docs", Ok() ], results)

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a cancelled change leaves the archive exactly as it was`` extension =
    let fs = tree ()
    let archive = fp (at $"start/data.{extension}")
    packed fs archive
    let before = fs.File.ReadAllBytes(FileSystemPath.value archive)

    let stopped =
        { Operations.unattended with
            IsCancelled = fun () -> true }

    Archive.addEntries fs stopped archive ArchivePath.root [ fp (at "loose/added.txt") ]
    |> Expect.ok
    |> ignore

    Assert.Equal<byte array>(before, fs.File.ReadAllBytes(FileSystemPath.value archive))
    Assert.Equal<string list>([ "docs"; "notes.txt" ], names fs archive ArchivePath.root)

[<Theory>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
[<InlineData("7z")>]
let ``a kind that can be changed takes a directory of its own`` extension =
    let fs = tree ()
    let archive = fp (at $"start/data.{extension}")
    packed fs archive

    Archive.makeEntryDirectory fs archive ArchivePath.root (en "fresh")
    |> Expect.ok

    Assert.Equal<string list>([ "docs"; "fresh"; "notes.txt" ], names fs archive ArchivePath.root)
