module Marbas.Tests.FileSystem.DirectorySizeTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private inside raw = ArchivePath.tryCreate raw |> Expect.some

let private zip = ArchiveFormat.ofKind Zip |> Expect.some

let private archive = at "backup/data.zip"

let private tree () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "data/a.txt", MockFileData "12345")
    fs.AddFile(at "data/docs/b.txt", MockFileData "1234567890")
    fs.AddFile(at "data/docs/deep/c.txt", MockFileData "123")
    fs.AddDirectory(at "data/docs/empty")
    fs

let private packed () =
    let fs = tree ()

    let request =
        { Path = fp archive
          Format = zip
          Compression = Normal
          Password = None }

    Archive.write fs Operations.unattended request [ fp (at "data/a.txt"); fp (at "data/docs") ]
    |> ignore
    fs

let private measured (fs: IFileSystem) location =
    DirectorySize.measure fs Operations.unattended location

let private recording (reported: ResizeArray<FileSystemPath>) : Operations.BatchControl =
    { Report = reported.Add
      IsCancelled = fun () -> false }

let private stoppingAfter count (reported: ResizeArray<FileSystemPath>) : Operations.BatchControl =
    { Report = reported.Add
      IsCancelled = fun () -> reported.Count >= count }

let private paths (reported: ResizeArray<FileSystemPath>) =
    reported |> Seq.map FileSystemPath.value |> List.ofSeq

[<Fact>]
let ``a tree measures to the byte`` () =
    let fs = tree ()

    let size = measured fs (OnDisk(fp (at "data")))

    Assert.Equal(Exact 18L, size)

[<Fact>]
let ``a directory holding nothing measures nothing`` () =
    let fs = tree ()

    let size = measured fs (OnDisk(fp (at "data/docs/empty")))

    Assert.Equal(Exact 0L, size)

[<Fact>]
let ``every directory below is reported as it is entered`` () =
    let fs = tree ()
    let reported = ResizeArray<FileSystemPath>()

    DirectorySize.measure fs (recording reported) (OnDisk(fp (at "data"))) |> ignore

    Assert.Equal<string list>(
        [ at "data"; at "data/docs"; at "data/docs/deep"; at "data/docs/empty" ],
        paths reported |> List.sort
    )

[<Fact>]
let ``a directory that cannot be read answers a lower bound`` () =
    let fs = Fakes.UnreadableDirectory(at "data/docs")
    fs.AddFile(at "data/a.txt", MockFileData "12345")
    fs.AddFile(at "data/docs/b.txt", MockFileData "1234567890")

    let size = measured fs (OnDisk(fp (at "data")))

    Assert.Equal(AtLeast 5L, size)

[<Fact>]
let ``a walk stopped part way answers what it had counted`` () =
    let fs = tree ()
    let reported = ResizeArray<FileSystemPath>()

    let size =
        DirectorySize.measure fs (stoppingAfter 1 reported) (OnDisk(fp (at "data")))

    Assert.Equal(AtLeast 5L, size)

[<Fact>]
let ``a directory link is a leaf rather than a way in`` () =
    let fs = tree ()
    fs.Directory.CreateSymbolicLink(at "data/twin", at "data/docs") |> ignore
    let reported = ResizeArray<FileSystemPath>()

    let size = DirectorySize.measure fs (recording reported) (OnDisk(fp (at "data")))

    Assert.Equal(Exact 18L, size)
    Assert.DoesNotContain(at "data/twin", paths reported)

[<Fact>]
let ``a missing directory answers a lower bound of nothing`` () =
    let fs = tree ()

    let size = measured fs (OnDisk(fp (at "data/gone")))

    Assert.Equal(AtLeast 0L, size)

[<Fact>]
let ``an archive measures what its entries hold unpacked`` () =
    let fs = packed ()

    let size = measured fs (InArchive(fp archive, ArchivePath.root))

    Assert.Equal(Exact 18L, size)

[<Fact>]
let ``a level inside an archive measures only what is under it`` () =
    let fs = packed ()

    let size = measured fs (InArchive(fp archive, inside "docs"))

    Assert.Equal(Exact 13L, size)

[<Fact>]
let ``every level of an archive is reported as it is entered`` () =
    let fs = packed ()
    let reported = ResizeArray<FileSystemPath>()

    DirectorySize.measure fs (recording reported) (InArchive(fp archive, ArchivePath.root))
    |> ignore

    Assert.Equal<string list>(
        [ at "backup/data.zip"
          at "backup/data.zip/docs"
          at "backup/data.zip/docs/deep"
          at "backup/data.zip/docs/empty" ],
        paths reported |> List.sort
    )

[<Fact>]
let ``an archive Marbas cannot read answers a lower bound of nothing`` () =
    let fs = tree ()

    let size = measured fs (InArchive(fp (at "data/a.txt"), ArchivePath.root))

    Assert.Equal(AtLeast 0L, size)
