module Marbas.Tests.FileSystem.DirectoryListingTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``lists files and directories of the given path`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    let entries = read fs (fp (at "data")) |> Expect.ok

    Assert.Equal<string list>([ "docs"; "notes.txt" ], entries |> List.map (_.Name >> EntryName.value) |> List.sort)

[<Fact>]
let ``directories have no size and files report theirs`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    let entries = read fs (fp (at "data")) |> Expect.ok
    let byName name = entries |> List.find (fun e -> e.Name = EntryName.create name)

    Assert.Equal(Directory, (byName "docs").Kind)
    Assert.Equal(None, (byName "docs").Size)
    Assert.Equal(File, (byName "notes.txt").Kind)
    Assert.Equal(Some 5L, (byName "notes.txt").Size)

[<Fact>]
let ``does not descend into nested directories`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")

    let entries = read fs (fp (at "data")) |> Expect.ok

    Assert.Equal<string list>([ "docs" ], entries |> List.map (_.Name >> EntryName.value))

[<Fact>]
let ``reports last modification time`` () =
    let fs = MockFileSystem()
    let modified = DateTime(2026, 7, 1, 12, 30, 0)
    let file = MockFileData "x"
    file.LastWriteTime <- DateTimeOffset modified
    fs.AddFile(at "data/notes.txt", file)

    let entries = read fs (fp (at "data")) |> Expect.ok

    Assert.Equal(modified, (List.exactlyOne entries).Modified)

[<Fact>]
let ``a directory link carries what it points at`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    fs.Directory.CreateSymbolicLink(at "data/twin", at "data/docs") |> ignore

    let entries = read fs (fp (at "data")) |> Expect.ok
    let twin = entries |> List.find (fun e -> e.Name = EntryName.create "twin")

    Assert.Equal(Directory, twin.Kind)
    Assert.Equal(Some(fp (at "data/docs")), twin.LinkTarget)

[<Fact>]
let ``a file link carries what it points at`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")
    fs.File.CreateSymbolicLink(at "data/twin.txt", at "data/notes.txt") |> ignore

    let entries = read fs (fp (at "data")) |> Expect.ok
    let twin = entries |> List.find (fun e -> e.Name = EntryName.create "twin.txt")

    Assert.Equal(File, twin.Kind)
    Assert.Equal(Some(fp (at "data/notes.txt")), twin.LinkTarget)

[<Fact>]
let ``an entry that is no link points at nothing`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    let entries = read fs (fp (at "data")) |> Expect.ok

    Assert.Equal<FileSystemPath option list>([ None; None ], entries |> List.map _.LinkTarget)

[<Fact>]
let ``missing directory is an error value, not an exception`` () =
    let fs = MockFileSystem()

    let result = read fs (fp (at "nope"))

    Assert.Equal(Error(DirectoryNotFound(fp (at "nope"))), result)

[<Fact>]
let ``access denied maps to an error value`` () =
    let denied = UnauthorizedAccessException()

    let listingError = mapException (fp (at "locked")) denied

    Assert.Equal(AccessDenied(fp (at "locked")), listingError)

[<Fact>]
let ``unexpected failures map to an error value`` () =
    let failure = IO.IOException "boom"

    let listingError = mapException (fp (at "bad")) failure

    Assert.Equal(ListingFailed(fp (at "bad"), "boom"), listingError)
