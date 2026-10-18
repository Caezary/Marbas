module Marbas.Tests.FileSystem.Archives.ArchiveIndexTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Archives
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private zip = ArchiveFormat.tryCreate Zip |> Expect.some

let private archive = at "backup/data.zip"

let private inside raw =
    ArchivePath.tryCreate raw |> Expect.some

let private request path =
    { Path = fp path
      Format = zip
      Compression = Normal
      Password = None }

let private tree () =
    let fs = Fakes.ReadCounting()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/later.txt", MockFileData "later")
    fs

let private packing (fs: IFileSystem) sources =
    Archive.write fs Operations.unattended (request archive) (sources |> List.map (at >> fp))
    |> ignore

let private packed () =
    let fs = tree ()
    packing fs [ "work/notes.txt"; "work/docs" ]
    fs

let private keyOf (info: ArchiveEntryInfo) =
    info.Segments |> List.map EntryName.value |> String.concat "/"

let private names items =
    items |> List.map keyOf |> List.sort |> String.concat ","

[<Fact>]
let ``an archive is read once for every level a panel walks into`` () =
    let fs = packed ()
    ArchiveListing.read fs (fp archive) ArchivePath.root |> ignore

    ArchiveListing.read fs (fp archive) (inside "docs") |> ignore

    Assert.Equal(1, fs.Reads archive)

[<Fact>]
let ``a level walked into is listed from the index`` () =
    let fs = packed ()
    ArchiveIndex.read fs (fp archive) |> ignore

    let entries = ArchiveListing.read fs (fp archive) (inside "docs") |> Expect.ok

    Assert.Equal<string list>([ "inner.txt" ], entries |> List.map (_.Name >> EntryName.value))

[<Fact>]
let ``an archive written again under the same name is read again`` () =
    let fs = packed ()
    ArchiveIndex.read fs (fp archive) |> ignore
    fs.File.Delete archive
    packing fs [ "work/later.txt" ]

    let items = ArchiveIndex.read fs (fp archive) |> Expect.ok

    Assert.Equal("later.txt", names items)
    Assert.Equal(2, fs.Reads archive)

[<Fact>]
let ``two file systems keep their own index of one name`` () =
    let first = packed ()
    ArchiveIndex.read first (fp archive) |> ignore
    let second = tree ()
    packing second [ "work/later.txt" ]

    let items = ArchiveIndex.read second (fp archive) |> Expect.ok

    Assert.Equal("later.txt", names items)

[<Fact>]
let ``a name no readable kind owns has no index`` () =
    let fs = tree ()
    fs.AddFile(at "backup/data.bin", MockFileData "whatever")

    let refusal = ArchiveIndex.read fs (fp (at "backup/data.bin")) |> Expect.error

    Assert.Equal(KindCannotBeRead(fp (at "backup/data.bin")), refusal)

[<Fact>]
let ``an archive that is not there is reported as missing`` () =
    let fs = tree ()

    let refusal = ArchiveIndex.read fs (fp archive) |> Expect.error

    Assert.Equal(ArchiveNotFound(fp archive), refusal)

[<Fact>]
let ``an index tells a stored directory from a stored file`` () =
    let fs = packed ()

    let items = ArchiveIndex.read fs (fp archive) |> Expect.ok

    Assert.Equal(
        "docs/inner.txt=StoredFile,docs=StoredDirectory,notes.txt=StoredFile",
        items
        |> List.map (fun info -> $"{keyOf info}={info.Kind}")
        |> List.sort
        |> String.concat ","
    )
