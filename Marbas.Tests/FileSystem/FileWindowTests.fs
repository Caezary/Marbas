module Marbas.Tests.FileSystem.FileWindowTests

open System
open System.Text
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private notes = at "start/notes.txt"

let private withFile (data: MockFileData) =
    let fs = MockFileSystem()
    fs.AddFile(notes, data)
    fs

let private opened (fs: MockFileSystem) =
    FileWindow.openFile fs (fp notes) |> Expect.ok

let private failedPath error =
    match error with
    | ReadFailed(path, _) -> Some path
    | _ -> None

let private archive = at "backup/data.zip"

let private inside raw = ArchivePath.tryCreate raw |> Expect.some

let private request password =
    { Path = fp archive
      Format = ArchiveFormat.ofKind Zip |> Expect.some
      Compression = Normal
      Password = password }

let private packedInto password =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")

    Archive.write fs Operations.unattended (request password) [ fp (at "work/notes.txt"); fp (at "work/docs") ]
    |> ignore

    fs

let private packedArchive () = packedInto None

let private lockedArchive secret = packedInto (ArchivePassword.tryCreate secret)

[<Fact>]
let ``a source knows the path and length of its file`` () =
    let fs = withFile (MockFileData "first\nsecond")

    use source = opened fs

    Assert.Equal(fp notes, source.Path)
    Assert.Equal(12L, source.Length)

[<Fact>]
let ``a range is read from where it was asked for`` () =
    let fs = withFile (MockFileData "first\nsecond")

    use source = opened fs

    Assert.Equal("second", Encoding.ASCII.GetString(source.Read(6L, 6)))

[<Fact>]
let ``a range reaching past the end stops at the end`` () =
    let fs = withFile (MockFileData "first")

    use source = opened fs

    Assert.Equal("rst", Encoding.ASCII.GetString(source.Read(2L, 400)))

[<Fact>]
let ``a range starting past the end is empty`` () =
    let fs = withFile (MockFileData "first")

    use source = opened fs

    Assert.Empty(source.Read(80L, 16))

[<Fact>]
let ``ranges can be read in any order`` () =
    let fs = withFile (MockFileData "first\nsecond")

    use source = opened fs
    let second = Encoding.ASCII.GetString(source.Read(6L, 6))
    let first = Encoding.ASCII.GetString(source.Read(0L, 5))

    Assert.Equal<string list>([ "second"; "first" ], [ second; first ])

[<Fact>]
let ``the encoding is settled when the file is opened`` () =
    let marked = Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes "first")
    let fs = withFile (MockFileData marked)

    use source = opened fs

    Assert.Equal({ Charset = Utf16Le; HasBom = true }, source.Encoding)

[<Fact>]
let ``an empty file opens and reads as nothing`` () =
    let fs = withFile (MockFileData "")

    use source = opened fs

    Assert.Equal(0L, source.Length)
    Assert.Empty(source.Read(0L, 16))

[<Fact>]
let ``a file larger than the probe is opened without reading it whole`` () =
    let fs = withFile (MockFileData(String('x', TextEncoding.ProbeLength * 4)))

    use source = opened fs

    Assert.Equal(int64 TextEncoding.ProbeLength * 4L, source.Length)
    Assert.Equal({ Charset = Utf8; HasBom = false }, source.Encoding)

[<Fact>]
let ``a missing file is an error value, not an exception`` () =
    let fs = MockFileSystem()

    let failure = FileWindow.openFile fs (fp notes) |> Expect.error

    Assert.Equal(FileNotFound(fp notes), failure)

[<Fact>]
let ``a directory is not a file to open`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")

    let failure = FileWindow.openFile fs (fp (at "start/docs")) |> Expect.error

    Assert.Equal(FileNotFound(fp (at "start/docs")), failure)

[<Fact>]
let ``a file that cannot be opened is an error value, not an exception`` () =
    let held = MockFileData "first"
    held.AllowedFileShare <- IO.FileShare.None
    let fs = withFile held

    let failure = FileWindow.openFile fs (fp notes) |> Expect.error

    Assert.Equal(Some(fp notes), failedPath failure)

[<Fact>]
let ``an entry of an archive is opened under the name of its place in it`` () =
    let fs = packedArchive ()

    use source = FileWindow.openEntry fs None (fp archive) (inside "docs/inner.txt") |> Expect.ok

    Assert.Equal(fp (at "backup/data.zip/docs/inner.txt"), source.Path)
    Assert.Equal("inner", Encoding.ASCII.GetString(source.Read(0L, 5)))

[<Fact>]
let ``an entry at the top of an archive is opened as well`` () =
    let fs = packedArchive ()

    use source = FileWindow.openEntry fs None (fp archive) (inside "notes.txt") |> Expect.ok

    Assert.Equal(5L, source.Length)
    Assert.Equal("notes", Encoding.ASCII.GetString(source.Read(0L, 5)))

[<Fact>]
let ``an entry the archive does not hold is refused under its own name`` () =
    let fs = packedArchive ()

    let error = FileWindow.openEntry fs None (fp archive) (inside "gone.txt") |> Expect.error

    Assert.Equal(ArchiveNotFound(fp (at "backup/data.zip/gone.txt")), error)

[<Fact>]
let ``an entry of a name that is no archive is refused before the file is opened`` () =
    let fs = packedArchive ()
    fs.AddFile(at "backup/data.bin", MockFileData "whatever")

    let error =
        FileWindow.openEntry fs None (fp (at "backup/data.bin")) (inside "notes.txt") |> Expect.error

    Assert.Equal(KindCannotBeRead(fp (at "backup/data.bin")), error)

[<Fact>]
let ``an entry of a locked archive is opened with the password`` () =
    let fs = lockedArchive "opensesame"

    use source =
        FileWindow.openEntry fs (ArchivePassword.tryCreate "opensesame") (fp archive) (inside "notes.txt")
        |> Expect.ok

    Assert.Equal("notes", Encoding.ASCII.GetString(source.Read(0L, 5)))
