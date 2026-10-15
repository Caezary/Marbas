module Marbas.Tests.FileSystem.WipeTests

open System.IO
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private secret = Array.init 1000 (fun index -> byte (index % 251))

let private stoppingAfter count (reported: ResizeArray<FileSystemPath>) =
    { Report = reported.Add
      IsCancelled = fun () -> reported.Count >= count }

let private recording (reported: ResizeArray<FileSystemPath>) =
    { Report = reported.Add
      IsCancelled = fun () -> false }

let private entriesIn (fs: IFileSystem) (raw: string) =
    fs.Directory.GetFileSystemEntries raw |> List.ofArray

let private fileName (raw: string) = Path.GetFileName raw |> Expect.notNull

[<Fact>]
let ``wipe removes a file and leaves nothing beside it`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/secret.txt", MockFileData secret)

    wipe fs unattended (fp (at "data/secret.txt")) |> Expect.ok

    Assert.Empty(entriesIn fs (at "data"))

[<Fact>]
let ``the contents are replaced over their whole length before the entry is removed`` () =
    let fs = Fakes.WipeWitness()
    fs.AddFile(at "data/secret.txt", MockFileData secret)

    wipe fs unattended (fp (at "data/secret.txt")) |> Expect.ok

    let overwritten = List.head fs.Flushed
    Assert.Equal(secret.Length, overwritten.Length)
    Assert.NotEqual<byte array>(secret, overwritten)
    Assert.False(fs.File.Exists(at "data/secret.txt"))

[<Fact>]
let ``the file is emptied before it is renamed`` () =
    let fs = Fakes.WipeWitness()
    fs.AddFile(at "data/secret.txt", MockFileData secret)

    wipe fs unattended (fp (at "data/secret.txt")) |> Expect.ok

    Assert.Empty(List.last fs.Flushed)

[<Fact>]
let ``the file is renamed to a name of its own length in its own directory`` () =
    let fs = Fakes.WipeWitness()
    fs.AddFile(at "data/secret.txt", MockFileData secret)

    wipe fs unattended (fp (at "data/secret.txt")) |> Expect.ok

    let source, target = List.exactlyOne fs.Renamed
    Assert.Equal(at "data/secret.txt", source)
    Assert.Equal(at "data", Expect.notNull (Path.GetDirectoryName target))
    Assert.Equal("secret.txt".Length, (fileName target).Length)
    Assert.NotEqual<string>("secret.txt", fileName target)

[<Fact>]
let ``a read-only file is wiped rather than refused`` () =
    let fs = MockFileSystem()
    let guarded = MockFileData secret
    guarded.Attributes <- FileAttributes.ReadOnly
    fs.AddFile(at "data/secret.txt", guarded)

    wipe fs unattended (fp (at "data/secret.txt")) |> Expect.ok

    Assert.Empty(entriesIn fs (at "data"))

[<Fact>]
let ``a directory is removed after everything that was in it`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/a.txt", MockFileData "a")
    fs.AddFile(at "data/docs/deep/b.txt", MockFileData "b")
    fs.AddDirectory(at "data/docs/deep/empty")
    fs.AddFile(at "data/kept.txt", MockFileData "kept")

    wipe fs unattended (fp (at "data/docs")) |> Expect.ok

    Assert.Equal<string list>([ at "data/kept.txt" ], entriesIn fs (at "data"))

[<Fact>]
let ``every file of a directory has its contents replaced`` () =
    let fs = Fakes.WipeWitness()
    fs.AddFile(at "data/docs/a.txt", MockFileData "aaaa")
    fs.AddFile(at "data/docs/deep/b.txt", MockFileData "bbbbbbbb")

    wipe fs unattended (fp (at "data/docs")) |> Expect.ok

    Assert.Equal<int list>([ 0; 0; 4; 8 ], fs.Flushed |> List.map Array.length |> List.sort)

[<Fact>]
let ``a file is reported at each buffer it is overwritten with`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/large.bin", MockFileData(Array.zeroCreate (2 * WipeBufferLength + 1)))
    let reported = ResizeArray<FileSystemPath>()

    wipe fs (recording reported) (fp (at "data/large.bin")) |> Expect.ok

    Assert.Equal<FileSystemPath list>(List.replicate 3 (fp (at "data/large.bin")), List.ofSeq reported)

[<Fact>]
let ``a cancelled run names the file it had begun`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/secret.txt", MockFileData secret)
    let reported = ResizeArray<FileSystemPath>()

    let failure =
        wipe fs (stoppingAfter 1 reported) (fp (at "data/docs")) |> Expect.error

    Assert.Equal(Cancelled(fp (at "data/docs/secret.txt")), failure)

[<Fact>]
let ``a cancelled run leaves the file it had begun where it is, its contents gone`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/secret.txt", MockFileData secret)
    let reported = ResizeArray<FileSystemPath>()

    wipe fs (stoppingAfter 1 reported) (fp (at "data/secret.txt")) |> ignore

    let left = fs.File.ReadAllBytes(at "data/secret.txt")
    Assert.Equal(secret.Length, left.Length)
    Assert.NotEqual<byte array>(secret, left)

[<Fact>]
let ``a directory link is removed without touching what it points at`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/secret.txt", MockFileData secret)
    fs.Directory.CreateSymbolicLink(at "data/twin", at "data/docs") |> ignore

    wipe fs unattended (fp (at "data/twin")) |> Expect.ok

    Assert.False(fs.Directory.Exists(at "data/twin"))
    Assert.Equal<byte array>(secret, fs.File.ReadAllBytes(at "data/docs/secret.txt"))

[<Fact>]
let ``a tree holding a link to its own ancestor is wiped without walking through the link`` () =
    let fs = Fakes.LinkFollowing()
    fs.AddFile(at "data/docs/secret.txt", MockFileData secret)
    fs.AddFile(at "data/kept.txt", MockFileData secret)
    fs.Directory.CreateSymbolicLink(at "data/docs/loop", at "data") |> ignore

    wipe fs unattended (fp (at "data/docs")) |> Expect.ok

    Assert.False(fs.Directory.Exists(at "data/docs"))
    Assert.Equal<byte array>(secret, fs.File.ReadAllBytes(at "data/kept.txt"))

[<Fact>]
let ``a file link is removed without touching what it points at`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/secret.txt", MockFileData secret)
    fs.File.CreateSymbolicLink(at "data/twin.txt", at "data/secret.txt") |> ignore

    wipe fs unattended (fp (at "data/twin.txt")) |> Expect.ok

    Assert.False(fs.File.Exists(at "data/twin.txt"))
    Assert.Equal<byte array>(secret, fs.File.ReadAllBytes(at "data/secret.txt"))

[<Fact>]
let ``wipe reports a missing source`` () =
    let fs = MockFileSystem()

    let failure = wipe fs unattended (fp (at "data/gone.txt")) |> Expect.error

    Assert.Equal(SourceNotFound(fp (at "data/gone.txt")), failure)

[<Fact>]
let ``wipeAll wipes every source it is given and stops where it is cancelled`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/a.txt", MockFileData "a")
    fs.AddFile(at "data/b.txt", MockFileData "b")
    let reported = ResizeArray<FileSystemPath>()

    let wiped =
        wipeAll fs (stoppingAfter 1 reported) [ fp (at "data/a.txt"); fp (at "data/b.txt") ]

    Assert.Equal<Operations.ItemSource list>(
        [ Operations.ItemOnDisk(fp (at "data/a.txt")) ],
        wiped |> List.map _.Source
    )

    Assert.True(fs.File.Exists(at "data/b.txt"))

let private watched () =
    let fs = Fakes.WalkWitness()
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddFile(at "work/docs/deep/leaf.txt", MockFileData "leaf")
    fs

[<Fact>]
let ``wiping a tree enumerates each directory once`` () =
    let fs = watched ()

    wipe fs unattended (fp (at "work")) |> Expect.ok

    Assert.Equal(3, fs.Enumerations)

[<Fact>]
let ``wiping a tree asks nothing about an entry it enumerated`` () =
    let fs = watched ()

    wipe fs unattended (fp (at "work")) |> Expect.ok

    Assert.Equal(0, fs.AskedAbout(at "work/docs/inner.txt"))
