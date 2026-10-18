module Marbas.Tests.Locations.LandingTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Archives
open Marbas.Locations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private inside raw =
    ArchivePath.tryCreate raw |> Expect.some

let private archive = fp (at "start/backup.zip")

let private packed () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")

    Archive.write
        fs
        Operations.unattended
        { Path = archive
          Format = ArchiveFormat.tryCreate Zip |> Expect.some
          Compression = Normal
          Password = None }
        [ fp (at "work/notes.txt"); fp (at "work/docs") ]
    |> ignore

    fs

[<Fact>]
let ``a directory on disk is landed in`` () =
    let fs = packed ()

    let landing = Landing.ofPath fs (fp (at "work/docs"))

    Assert.Equal(Some(Into(OnDisk(fp (at "work/docs")))), landing)

[<Fact>]
let ``a file on disk is landed beside`` () =
    let fs = packed ()

    let landing = Landing.ofPath fs (fp (at "work/notes.txt"))

    Assert.Equal(Some(Beside(OnDisk(fp (at "work")), en "notes.txt")), landing)

[<Fact>]
let ``an archive itself is landed beside, as any file is`` () =
    let fs = packed ()

    let landing = Landing.ofPath fs archive

    Assert.Equal(Some(Beside(OnDisk(fp (at "start")), en "backup.zip")), landing)

[<Theory>]
[<InlineData("docs")>]
[<InlineData("docs/deep")>]
let ``a path to a level inside an archive opens the archive at that level`` (level: string) =
    let fs = packed ()

    let landing = Landing.ofPath fs (ArchivePath.spelled fs archive (inside level))

    Assert.Equal(Some(Into(InArchive(archive, inside level))), landing)

[<Theory>]
[<InlineData("", "notes.txt")>]
[<InlineData("docs", "inner.txt")>]
[<InlineData("docs/deep", "leaf.txt")>]
let ``a path to an entry inside an archive opens its level with the cursor on it`` (level: string, name: string) =
    let fs = packed ()
    let entry = ArchivePath.child (inside level) (en name)

    let landing = Landing.ofPath fs (ArchivePath.spelled fs archive entry)

    Assert.Equal(Some(Beside(InArchive(archive, inside level), en name)), landing)

[<Theory>]
[<InlineData("gone.txt")>]
[<InlineData("docs/gone.txt")>]
[<InlineData("gone/inner.txt")>]
[<InlineData("notes.txt/below")>]
let ``a path into an archive that does not hold it lands nowhere`` (entry: string) =
    let fs = packed ()

    let landing = Landing.ofPath fs (ArchivePath.spelled fs archive (inside entry))

    Assert.Equal(None, landing)

[<Fact>]
let ``a path through a file that is no archive lands nowhere`` () =
    let fs = packed ()

    let landing = Landing.ofPath fs (fp (at "work/notes.txt/inner.txt"))

    Assert.Equal(None, landing)

[<Fact>]
let ``a path through an archive no reader can open lands nowhere`` () =
    let fs = packed ()
    fs.AddFile(at "start/broken.zip", MockFileData "not a zip")

    let landing = Landing.ofPath fs (fp (at "start/broken.zip/docs"))

    Assert.Equal(None, landing)

[<Fact>]
let ``a path below a directory that does not hold it lands nowhere`` () =
    let fs = packed ()

    let landing = Landing.ofPath fs (fp (at "start/gone/deeper"))

    Assert.Equal(None, landing)

[<Fact>]
let ``a directory kept as a place is landed in`` () =
    let place = Place.OnDisk(fp (at "work/docs"))

    let landing = Landing.inside place

    Assert.Equal(Into(OnDisk(fp (at "work/docs"))), landing)

[<Theory>]
[<InlineData("")>]
[<InlineData("docs/deep")>]
let ``a level of an archive kept as a place is landed in`` (level: string) =
    let place = Place.InArchive(archive, inside level)

    let landing = Landing.inside place

    Assert.Equal(Into(InArchive(archive, inside level)), landing)

[<Fact>]
let ``a file kept as a place is landed beside, in the directory holding it`` () =
    let fs = MockFileSystem()
    let place = Place.OnDisk(fp (at "work/notes.txt"))

    let landing = Landing.beside fs place

    Assert.Equal(Some(Beside(OnDisk(fp (at "work")), en "notes.txt")), landing)

[<Theory>]
[<InlineData("", "notes.txt")>]
[<InlineData("docs", "inner.txt")>]
[<InlineData("docs/deep", "leaf.txt")>]
let ``an entry of an archive kept as a place is landed beside, at its level`` (level: string, name: string) =
    let fs = MockFileSystem()
    let place = Place.InArchive(archive, ArchivePath.child (inside level) (en name))

    let landing = Landing.beside fs place

    Assert.Equal(Some(Beside(InArchive(archive, inside level), en name)), landing)

[<Fact>]
let ``the root of an archive kept as a place has nothing to be landed beside`` () =
    let fs = MockFileSystem()
    let place = Place.InArchive(archive, ArchivePath.root)

    let landing = Landing.beside fs place

    Assert.Equal(None, landing)
