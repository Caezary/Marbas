module Marbas.Tests.FileSystem.FileContentTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.FileContent
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private notes = at "start/notes.txt"

let private withFile (data: MockFileData) =
    let fs = MockFileSystem()
    fs.AddFile(notes, data)
    fs

let private failedPath error =
    match error with
    | ReadFailed(path, _) -> Some path
    | _ -> None

[<Fact>]
let ``a text file is read line by line`` () =
    let fs = withFile (MockFileData "first\r\nsecond")

    let content = read fs (fp notes) |> Expect.ok

    Assert.Equal(Text [ "first"; "second" ], content.Body)
    Assert.Equal(Whole, content.Extent)

[<Fact>]
let ``every line ending splits a line`` () =
    let fs = withFile (MockFileData "first\nsecond\r\nthird\rfourth")

    let content = read fs (fp notes) |> Expect.ok

    Assert.Equal(Text [ "first"; "second"; "third"; "fourth" ], content.Body)

[<Fact>]
let ``a trailing line ending adds no empty line`` () =
    let fs = withFile (MockFileData "first\nsecond\n")

    let content = read fs (fp notes) |> Expect.ok

    Assert.Equal(Text [ "first"; "second" ], content.Body)

[<Fact>]
let ``an empty file has no lines`` () =
    let fs = withFile (MockFileData "")

    let content = read fs (fp notes) |> Expect.ok

    Assert.Equal(Text [], content.Body)

[<Fact>]
let ``a file holding a zero byte is read as bytes`` () =
    let bytes = [| 72uy; 105uy; 0uy; 10uy |]
    let fs = withFile (MockFileData bytes)

    let content = read fs (fp notes) |> Expect.ok

    Assert.Equal(Binary bytes, content.Body)

[<Fact>]
let ``a file beyond the limit is read up to it and reported as cut short`` () =
    let bytes = Array.zeroCreate (MaxBytes + 10)
    let fs = withFile (MockFileData bytes)

    let content = read fs (fp notes) |> Expect.ok

    Assert.Equal(Binary(Array.zeroCreate MaxBytes), content.Body)
    Assert.Equal(Truncated, content.Extent)

[<Fact>]
let ``a missing file is an error value, not an exception`` () =
    let fs = MockFileSystem()

    let result = read fs (fp notes)

    Assert.Equal(Error(FileNotFound(fp notes)), result)

[<Fact>]
let ``a directory is not a file to read`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")

    let result = read fs (fp (at "start/docs"))

    Assert.Equal(Error(FileNotFound(fp (at "start/docs"))), result)

[<Fact>]
let ``a file that cannot be opened is an error value, not an exception`` () =
    let held = MockFileData "first\nsecond"
    held.AllowedFileShare <- IO.FileShare.None
    let fs = withFile held

    let failure = read fs (fp notes) |> Expect.error

    Assert.Equal(Some(fp notes), failedPath failure)

[<Fact>]
let ``access denied maps to an error value`` () =
    let denied = UnauthorizedAccessException()

    let contentError = mapException (fp (at "locked.txt")) denied

    Assert.Equal(AccessDenied(fp (at "locked.txt")), contentError)

[<Fact>]
let ``unexpected failures map to an error value`` () =
    let failure = IO.IOException "boom"

    let contentError = mapException (fp (at "bad.txt")) failure

    Assert.Equal(ReadFailed(fp (at "bad.txt"), "boom"), contentError)
