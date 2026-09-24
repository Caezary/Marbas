module Marbas.Tests.FileSystem.ArchiveReaderTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private archive = at "backup/data.zip"

let private fs = MockFileSystem()

[<Fact>]
let ``a missing file maps to an archive that is not there`` () =
    let failure = IO.FileNotFoundException "gone"

    let error = ArchiveReader.mapException (fp archive) failure

    Assert.Equal(ArchiveNotFound(fp archive), error)

[<Fact>]
let ``a refused password maps to a password that is needed`` () =
    let failure = SharpCompress.Common.CryptographicException "wrong password"

    let error = ArchiveReader.mapException (fp archive) failure

    Assert.Equal(PasswordNeeded(fp archive), error)

[<Fact>]
let ``every other failure maps to a damaged archive`` () =
    let failure = IO.IOException "boom"

    let error = ArchiveReader.mapException (fp archive) failure

    Assert.Equal(ArchiveDamaged(fp archive, "boom"), error)

[<Fact>]
let ``a kind that claims it can be read is the kind of the file`` () =
    let kind = ArchiveReader.kindOf fs (fp archive)

    Assert.Equal(Some Zip, kind)

[<Theory>]
[<InlineData("backup/data.bin")>]
[<InlineData("backup/data.rar.txt")>]
[<InlineData("work/notes.txt")>]
let ``a name that is no archive is no kind to open`` raw =
    let kind = ArchiveReader.kindOf fs (fp (at raw))

    Assert.Equal(None, kind)

[<Theory>]
[<InlineData("docs/inner.txt", "docs,inner.txt")>]
[<InlineData("docs/", "docs")>]
[<InlineData("./docs/inner.txt", "docs,inner.txt")>]
[<InlineData("/docs/inner.txt", "docs,inner.txt")>]
[<InlineData("docs/../evil.txt", "docs,..,evil.txt")>]
let ``a key is read as the names it is made of`` key expected =
    let segments = ArchiveReader.segmentsOf key

    Assert.Equal(expected, segments |> List.map EntryName.value |> String.concat ",")

[<Fact>]
let ``a refused file maps to an archive that may not be touched`` () =
    let failure = UnauthorizedAccessException "denied"

    let error = ArchiveReader.mapException (fp archive) failure

    Assert.Equal(ArchiveAccessDenied(fp archive), error)

let private en = EntryName.create

let private zip = ArchiveFormat.tryCreate Zip |> Expect.some

let private packedIn (tree: MockFileSystem) =
    tree.AddDirectory(at "backup")
    tree.AddFile(at "work/notes.txt", MockFileData "notes")
    tree.AddFile(at "work/docs/inner.txt", MockFileData "inner")

    Archive.write
        tree
        Operations.unattended
        { Path = fp archive
          Format = zip
          Compression = Normal
          Password = None }
        [ fp (at "work/notes.txt"); fp (at "work/docs") ]
    |> ignore

    tree

let private packed () = packedIn (MockFileSystem())

let private walking (tree: MockFileSystem) (step: ArchiveStep) =
    ArchiveReader.withItems tree None Zip (fp archive) step

let private keyOf (info: ArchiveEntryInfo) =
    info.Segments |> List.map EntryName.value |> String.concat "/"

let private noting (seen: ResizeArray<string>) =
    ArchiveStep(fun item ->
        seen.Add(keyOf item.Info)
        StopReading)

let private writingNotes (target: IO.Stream) =
    ArchiveStep(fun item ->
        if item.Info.Segments = [ en "notes.txt" ] then
            use content = item.OpenContent()
            content.CopyTo target
            StopReading
        else
            KeepReading)

let private itemType =
    typeof<ArchiveEntryInfo>.Assembly.GetType "Marbas.FileSystem.ArchiveItem"

[<Fact>]
let ``an item handed to the walk may not outlive it`` () =
    let scoped = itemType.IsByRefLike

    Assert.True scoped

[<Fact>]
let ``the walk stops where the step says it should`` () =
    let tree = packed ()
    let seen = ResizeArray<string>()

    walking tree (noting seen)

    Assert.Equal(1, seen.Count)

[<Fact>]
let ``the content of an entry is written while the walk stands on it`` () =
    let tree = packed ()
    use content = new IO.MemoryStream()

    walking tree (writingNotes content)

    Assert.Equal("notes", Text.Encoding.UTF8.GetString(content.ToArray()))

let private textIn (content: IO.Stream) =
    use reader = new IO.StreamReader(content)
    reader.ReadToEnd()

[<Fact>]
let ``one entry is described without its content being touched`` () =
    let tree = packed ()

    let info =
        ArchiveReader.entryInfo tree None Zip (fp archive) [ en "docs"; en "inner.txt" ]
        |> Expect.some

    Assert.Equal(5L, info.Size)
    Assert.Equal(StoredFile, info.Kind)

[<Fact>]
let ``an entry the archive does not hold is described by nothing`` () =
    let tree = packed ()

    let info = ArchiveReader.entryInfo tree None Zip (fp archive) [ en "gone.txt" ]

    Assert.True(Option.isNone info)

[<Fact>]
let ``one entry is opened as a stream that outlives the walk`` () =
    let tree = packed ()

    use content =
        ArchiveReader.openContent tree None Zip (fp archive) [ en "docs"; en "inner.txt" ]
        |> Expect.some

    Assert.Equal("inner", textIn content)

[<Fact>]
let ``a directory entry is never opened as content`` () =
    let tree = packed ()

    let content = ArchiveReader.openContent tree None Zip (fp archive) [ en "docs" ]

    Assert.True(Option.isNone content)

[<Fact>]
let ``the archive file is closed again with the stream it handed out`` () =
    let tree = Fakes.ByteCounting()
    packedIn tree |> ignore
    let content = ArchiveReader.openContent tree None Zip (fp archive) [ en "notes.txt" ] |> Expect.some
    let before = tree.Closed

    content.Dispose()

    Assert.Equal(before + 1, tree.Closed)
