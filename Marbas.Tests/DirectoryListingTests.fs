module Marbas.Tests.DirectoryListingTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing

let private fp = FileSystemPath.create

let private ok result =
    match result with
    | Ok entries -> entries
    | Error err -> failwith $"expected Ok, got Error %A{err}"

[<Fact>]
let ``lists files and directories of the given path`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data\docs"
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")

    let entries = read fs (fp @"C:\data") |> ok

    let names = entries |> List.map (fun e -> e.Name) |> List.sort
    Assert.Equal<string list>([ "docs"; "notes.txt" ], names)

[<Fact>]
let ``directories have no size and files report theirs`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data\docs"
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")

    let entries = read fs (fp @"C:\data") |> ok
    let byName name = entries |> List.find (fun e -> e.Name = name)

    Assert.Equal(Directory, (byName "docs").Kind)
    Assert.Equal(None, (byName "docs").Size)
    Assert.Equal(File, (byName "notes.txt").Kind)
    Assert.Equal(Some 5L, (byName "notes.txt").Size)

[<Fact>]
let ``does not descend into nested directories`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\inner.txt", MockFileData "x")

    let entries = read fs (fp @"C:\data") |> ok

    Assert.Equal<string list>([ "docs" ], entries |> List.map (fun e -> e.Name))

[<Fact>]
let ``reports last modification time`` () =
    let fs = MockFileSystem()
    let modified = DateTime(2026, 7, 1, 12, 30, 0)
    let file = MockFileData "x"
    file.LastWriteTime <- DateTimeOffset modified
    fs.AddFile(@"C:\data\notes.txt", file)

    let entries = read fs (fp @"C:\data") |> ok

    Assert.Equal(modified, (entries |> List.exactlyOne).Modified)

[<Fact>]
let ``missing directory is an error value, not an exception`` () =
    let fs = MockFileSystem()

    Assert.Equal(Error(DirectoryNotFound(fp @"C:\nope")), read fs (fp @"C:\nope"))

[<Fact>]
let ``access denied maps to an error value`` () =
    Assert.Equal(
        AccessDenied(fp @"C:\locked"),
        mapException (fp @"C:\locked") (UnauthorizedAccessException()))

[<Fact>]
let ``unexpected failures map to an error value`` () =
    Assert.Equal(
        ListingFailed(fp @"C:\bad", "boom"),
        mapException (fp @"C:\bad") (IO.IOException "boom"))
