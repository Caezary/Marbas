module Marbas.Tests.FileSystem.Archives.BackslashNameTests

open System.IO
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open ICSharpCode.SharpZipLib.Zip
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Archives
open Marbas.FileSystem.Operations
open Marbas.Tests

[<Literal>]
let private WhereItIsAName =
    "A backslash is part of a name only where it does not separate"

[<Literal>]
let private UnixHost = 3

[<Literal>]
let private FatHost = 0

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private inside raw =
    ArchivePath.tryCreate raw |> Expect.some

let private archiveNamed extension = fp (at $"backup/data.{extension}")

let private zip = archiveNamed "zip"

let private plain () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddDirectory(at "out")
    fs.AddFile(at "work/plain.txt", MockFileData "plain")
    fs

let private planted () =
    let fs = plain ()
    fs.AddFile(at "work/a\\b.txt", MockFileData "odd")
    fs.AddFile(at "work/dir\\below/in\\side.txt", MockFileData "inner")
    fs.AddFile(at "work/end\\", MockFileData "ending")
    fs

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

let private adding (fs: IFileSystem) archive sources =
    Archive.addEntries fs unattended archive ArchivePath.root sources |> Expect.ok

let private unpacking (fs: IFileSystem) archive targets =
    Archive.extract
        fs
        unattended
        { Archive = archive
          Password = None
          Destination = fp (at "out") }
        targets
    |> Expect.ok
    |> ignore

let private keyedByHand (fs: IFileSystem) archive (keys: string list) =
    use stream = fs.File.Create(FileSystemPath.value archive)

    use held =
        new Compression.ZipArchive(stream, Compression.ZipArchiveMode.Create, true)

    keys |> List.iter (held.CreateEntry >> ignore)

let private stored (fs: IFileSystem) archive =
    ArchiveIndex.read fs archive
    |> Expect.ok
    |> List.map (_.Segments >> List.map EntryName.value)
    |> List.sort

let private sizeOf (fs: IFileSystem) archive name =
    ArchiveIndex.read fs archive
    |> Expect.ok
    |> List.find (fun info -> info.Segments = [ en name ])
    |> _.Size

let private hostOf (fs: IFileSystem) (name: string) =
    use stream = fs.File.OpenRead(FileSystemPath.value zip)
    use held = new ZipFile(stream)

    held.GetEntry(name).HostSystem

[<UnixTheory(WhereItIsAName)>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
let ``a file named with a backslash is packed as one entry`` extension =
    let fs = planted ()
    let archive = archiveNamed extension

    packing fs archive [ fp (at "work/a\\b.txt") ]

    Assert.Equal<string list list>([ [ "a\\b.txt" ] ], stored fs archive)

[<UnixTheory(WhereItIsAName)>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
let ``a directory named with a backslash is packed as one level`` extension =
    let fs = planted ()
    let archive = archiveNamed extension

    packing fs archive [ fp (at "work/dir\\below") ]

    Assert.Equal<string list list>([ [ "dir\\below" ]; [ "dir\\below"; "in\\side.txt" ] ], stored fs archive)

[<UnixTheory(WhereItIsAName)>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
let ``a file named with a backslash is added to an archive as one entry`` extension =
    let fs = planted ()
    let archive = archiveNamed extension
    packing fs archive [ fp (at "work/plain.txt") ]

    adding fs archive [ fp (at "work/a\\b.txt") ] |> ignore

    Assert.Equal<string list list>([ [ "a\\b.txt" ]; [ "plain.txt" ] ], stored fs archive)

[<UnixTheory(WhereItIsAName)>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
let ``a directory named with a backslash is added to an archive as one level`` extension =
    let fs = planted ()
    let archive = archiveNamed extension
    packing fs archive [ fp (at "work/plain.txt") ]

    adding fs archive [ fp (at "work/dir\\below") ] |> ignore

    Assert.Equal<string list list>(
        [ [ "dir\\below" ]; [ "dir\\below"; "in\\side.txt" ]; [ "plain.txt" ] ],
        stored fs archive
    )

[<UnixTheory(WhereItIsAName)>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
let ``a directory made inside an archive keeps the backslash typed into its name`` extension =
    let fs = planted ()
    let archive = archiveNamed extension
    packing fs archive [ fp (at "work/plain.txt") ]

    Archive.makeEntryDirectory fs archive ArchivePath.root (en "made\\here")
    |> Expect.ok

    Assert.Equal<string list list>([ [ "made\\here" ]; [ "plain.txt" ] ], stored fs archive)

[<UnixTheory(WhereItIsAName)>]
[<InlineData("zip")>]
[<InlineData("tar.gz")>]
let ``a file named with a backslash comes back out under its own name`` extension =
    let fs = planted ()
    let archive = archiveNamed extension
    packing fs archive [ fp (at "work/a\\b.txt") ]

    unpacking fs archive [ inside "a\\b.txt" ]

    Assert.Equal("odd", fs.File.ReadAllText(at "out/a\\b.txt"))

[<UnixFact(WhereItIsAName)>]
let ``a file packed into a zip on Unix says it was made there`` () =
    let fs = planted ()

    packing fs zip [ fp (at "work/a\\b.txt") ]

    Assert.Equal(UnixHost, hostOf fs "a\\b.txt")

[<UnixFact(WhereItIsAName)>]
let ``a directory packed into a zip on Unix says it was made there`` () =
    let fs = planted ()

    packing fs zip [ fp (at "work/dir\\below") ]

    Assert.Equal(UnixHost, hostOf fs "dir\\below/")

[<UnixFact(WhereItIsAName)>]
let ``a file added to a zip on Unix says it was made there`` () =
    let fs = planted ()
    packing fs zip [ fp (at "work/plain.txt") ]

    adding fs zip [ fp (at "work/a\\b.txt") ] |> ignore

    Assert.Equal(UnixHost, hostOf fs "a\\b.txt")

[<UnixFact(WhereItIsAName)>]
let ``a directory made inside a zip on Unix says it was made there`` () =
    let fs = planted ()
    packing fs zip [ fp (at "work/plain.txt") ]

    Archive.makeEntryDirectory fs zip ArchivePath.root (en "made\\here")
    |> Expect.ok

    Assert.Equal(UnixHost, hostOf fs "made\\here/")

[<WindowsFact "A zip packed on Windows says what it always said">]
let ``a file packed into a zip on Windows says nothing of Unix`` () =
    let fs = plain ()

    packing fs zip [ fp (at "work/plain.txt") ]

    Assert.Equal(FatHost, hostOf fs "plain.txt")

[<UnixFact(WhereItIsAName)>]
let ``a file whose name ends in a backslash is packed into a new zip with its content`` () =
    let fs = planted ()

    packing fs zip [ fp (at "work/end\\") ]

    Assert.Equal(6L, sizeOf fs zip "end\\")

[<UnixFact(WhereItIsAName)>]
let ``a file whose name ends in a backslash is refused by a zip that exists`` () =
    let fs = planted ()
    let source = fp (at "work/end\\")
    packing fs zip [ fp (at "work/plain.txt") ]

    let results = adding fs zip [ source ]

    Assert.Equal<ItemResult list>([ itemResult source (Error(NameNotAddable source)) ], results)

[<UnixFact(WhereItIsAName)>]
let ``a zip that refused a name holds what it held and what came with the name`` () =
    let fs = planted ()
    packing fs zip [ fp (at "work/plain.txt") ]

    adding fs zip [ fp (at "work/end\\"); fp (at "work/a\\b.txt") ] |> ignore

    Assert.Equal<string list list>([ [ "a\\b.txt" ]; [ "plain.txt" ] ], stored fs zip)

[<Fact>]
let ``an entry another tool named with a backslash keeps its name when something joins the zip`` () =
    let fs = plain ()
    keyedByHand fs zip [ "odd\\name.txt" ]

    adding fs zip [ fp (at "work/plain.txt") ] |> ignore

    Assert.Equal<string list list>([ [ "odd\\name.txt" ]; [ "plain.txt" ] ], stored fs zip)

[<Fact>]
let ``an entry another tool named with a backslash is removed under that name`` () =
    let fs = plain ()
    keyedByHand fs zip [ "odd\\name.txt"; "kept.txt" ]

    Archive.removeEntries fs unattended zip [ inside "odd\\name.txt" ]
    |> Expect.ok
    |> ignore

    Assert.Equal<string list list>([ [ "kept.txt" ] ], stored fs zip)
