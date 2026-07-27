module Marbas.Tests.FileSystemPathTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem

[<Fact>]
let ``value returns the wrapped string`` () =
    Assert.Equal(@"C:\data", FileSystemPath.create @"C:\data" |> FileSystemPath.value)

[<Fact>]
let ``equal paths compare equal`` () =
    Assert.Equal(FileSystemPath.create @"C:\data", FileSystemPath.create @"C:\data")

[<Fact>]
let ``combine appends a child name`` () =
    let fs = MockFileSystem()

    let combined = FileSystemPath.combine fs (FileSystemPath.create @"C:\data") "child"

    Assert.Equal(@"C:\data\child", FileSystemPath.value combined)

[<Fact>]
let ``name returns the last segment`` () =
    let fs = MockFileSystem()

    Assert.Equal("notes.txt", FileSystemPath.name fs (FileSystemPath.create @"C:\data\notes.txt"))

[<Fact>]
let ``directory returns the containing path`` () =
    let fs = MockFileSystem()

    let dir = FileSystemPath.directory fs (FileSystemPath.create @"C:\data\notes.txt")

    Assert.Equal(@"C:\data", FileSystemPath.value dir)

[<Fact>]
let ``parent returns the enclosing directory`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data\docs"

    match FileSystemPath.parent fs (FileSystemPath.create @"C:\data\docs") with
    | Some parent -> Assert.Equal(@"C:\data", FileSystemPath.value parent)
    | None -> failwith "expected a parent"

[<Fact>]
let ``parent of a root is None`` () =
    let fs = MockFileSystem()

    Assert.Equal(None, FileSystemPath.parent fs (FileSystemPath.create @"C:\"))
