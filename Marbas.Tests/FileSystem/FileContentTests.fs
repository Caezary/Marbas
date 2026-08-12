module Marbas.Tests.FileSystem.FileContentTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.FileContent
open Marbas.Tests

let private fp = FileSystemPath.create

let private notes = @"C:\start\notes.txt"

let private withFile (data: MockFileData) =
    let fs = MockFileSystem()
    fs.AddFile(notes, data)
    fs

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
    fs.AddDirectory @"C:\start\docs"

    let result = read fs (fp @"C:\start\docs")

    Assert.Equal(Error(FileNotFound(fp @"C:\start\docs")), result)

[<Fact>]
let ``access denied maps to an error value`` () =
    let denied = UnauthorizedAccessException()

    let contentError = mapException (fp @"C:\locked.txt") denied

    Assert.Equal(AccessDenied(fp @"C:\locked.txt"), contentError)

[<Fact>]
let ``unexpected failures map to an error value`` () =
    let failure = IO.IOException "boom"

    let contentError = mapException (fp @"C:\bad.txt") failure

    Assert.Equal(ReadFailed(fp @"C:\bad.txt", "boom"), contentError)
