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
