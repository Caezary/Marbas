module Marbas.Tests.DirectoryListingTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing

let private fp = FileSystemPath.create

[<Fact>]
let ``lists files and directories of the given path`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data\docs"
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")

    let entries = read fs (fp @"C:\data") |> Expect.ok

    Assert.Equal<string list>([ "docs"; "notes.txt" ], entries |> List.map _.Name |> List.sort)

[<Fact>]
let ``directories have no size and files report theirs`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data\docs"
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")

    let entries = read fs (fp @"C:\data") |> Expect.ok
    let byName name = entries |> List.find (fun e -> e.Name = name)

    Assert.Equal(Directory, (byName "docs").Kind)
    Assert.Equal(None, (byName "docs").Size)
    Assert.Equal(File, (byName "notes.txt").Kind)
    Assert.Equal(Some 5L, (byName "notes.txt").Size)

[<Fact>]
let ``does not descend into nested directories`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\docs\inner.txt", MockFileData "x")

    let entries = read fs (fp @"C:\data") |> Expect.ok

    Assert.Equal<string list>([ "docs" ], entries |> List.map _.Name)

[<Fact>]
let ``reports last modification time`` () =
    let fs = MockFileSystem()
    let modified = DateTime(2026, 7, 1, 12, 30, 0)
    let file = MockFileData "x"
    file.LastWriteTime <- DateTimeOffset modified
    fs.AddFile(@"C:\data\notes.txt", file)

    let entries = read fs (fp @"C:\data") |> Expect.ok

    Assert.Equal(modified, (List.exactlyOne entries).Modified)

[<Fact>]
let ``missing directory is an error value, not an exception`` () =
    let fs = MockFileSystem()

    let result = read fs (fp @"C:\nope")

    Assert.Equal(Error(DirectoryNotFound(fp @"C:\nope")), result)

[<Fact>]
let ``access denied maps to an error value`` () =
    let denied = UnauthorizedAccessException()

    let listingError = mapException (fp @"C:\locked") denied

    Assert.Equal(AccessDenied(fp @"C:\locked"), listingError)

[<Fact>]
let ``unexpected failures map to an error value`` () =
    let failure = IO.IOException "boom"

    let listingError = mapException (fp @"C:\bad") failure

    Assert.Equal(ListingFailed(fp @"C:\bad", "boom"), listingError)
