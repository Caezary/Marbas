module Marbas.Tests.FileSystem.FileSystemPathTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

[<Fact>]
let ``value returns the wrapped string`` () =
    let dataPath = FileSystemPath.create (at "data")

    let value = FileSystemPath.value dataPath

    Assert.Equal(at "data", value)

[<Fact>]
let ``equal paths compare equal`` () =
    let dataPath = FileSystemPath.create (at "data")

    let samePath = FileSystemPath.create (at "data")

    Assert.Equal(dataPath, samePath)

[<Fact>]
let ``combine appends a child name`` () =
    let fs = MockFileSystem()
    let parentFolder = FileSystemPath.create (at "data")

    let combined = FileSystemPath.combine fs parentFolder "child"

    Assert.Equal(at "data/child", FileSystemPath.value combined)

[<Fact>]
let ``name returns the last segment in a file path - its name`` () =
    let fs = MockFileSystem()
    let notesPath = FileSystemPath.create (at "data/notes.txt")

    let name = FileSystemPath.name fs notesPath

    Assert.Equal(EntryName.create "notes.txt", name)

[<Fact>]
let ``name returns the last segment in a directory path - its name`` () =
    let fs = MockFileSystem()
    let childPath = FileSystemPath.create (at "data/child")

    let name = FileSystemPath.name fs childPath

    Assert.Equal(EntryName.create "child", name)

[<Fact>]
let ``directory returns the containing path`` () =
    let fs = MockFileSystem()
    let notesPath = FileSystemPath.create (at "data/notes.txt")

    let containing = FileSystemPath.directory fs notesPath

    Assert.Equal(at "data", FileSystemPath.value containing)

[<Fact>]
let ``parent returns the enclosing directory`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    let docsPath = FileSystemPath.create (at "data/docs")

    let parent = FileSystemPath.parent fs docsPath

    Assert.Equal(Some (at "data"), parent |> Option.map FileSystemPath.value)

[<Fact>]
let ``parent of a root is None`` () =
    let fs = MockFileSystem()
    let rootPath = FileSystemPath.create (at "")

    let parent = FileSystemPath.parent fs rootPath

    Assert.Equal(None, parent)

[<Fact>]
let ``the nearest directory to one that is there is itself`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")

    let nearest = FileSystemPath.nearest fs (FileSystemPath.create (at "data/docs"))

    Assert.Equal(Some(at "data/docs"), nearest |> Option.map FileSystemPath.value)

[<Fact>]
let ``the nearest directory to one that is gone is the first ancestor still there`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data")

    let nearest = FileSystemPath.nearest fs (FileSystemPath.create (at "data/docs/deep"))

    Assert.Equal(Some(at "data"), nearest |> Option.map FileSystemPath.value)

[<Fact>]
let ``the nearest directory to a file is the directory holding it`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hi")

    let nearest = FileSystemPath.nearest fs (FileSystemPath.create (at "data/notes.txt"))

    Assert.Equal(Some(at "data"), nearest |> Option.map FileSystemPath.value)

[<WindowsFact>]
let ``a path whose whole chain is gone has no nearest directory`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data"

    let nearest = FileSystemPath.nearest fs (FileSystemPath.create @"D:\work\docs")

    Assert.Equal(None, nearest)

[<Fact>]
let ``a name without a directory part is a bare name`` () =
    let fs = MockFileSystem()

    let isBare = FileSystemPath.isBareName fs "notes.txt"

    Assert.True isBare

[<Fact>]
let ``a name carrying a directory is not a bare name`` () =
    let fs = MockFileSystem()

    let isBare = FileSystemPath.isBareName fs (Root.relative "docs/notes.txt")

    Assert.False isBare

[<Fact>]
let ``a rooted path is not a bare name`` () =
    let fs = MockFileSystem()

    let backupIsBare = FileSystemPath.isBareName fs (at "backup")
    let rootIsBare = FileSystemPath.isBareName fs (at "")

    Assert.False backupIsBare
    Assert.False rootIsBare

[<Fact>]
let ``kind is NoEntry for a missing path`` () =
    let fs = MockFileSystem()

    let kind = FileSystemPath.kind fs (FileSystemPath.create (at "data/missing.txt"))

    Assert.Equal(NoEntry, kind)

[<Fact>]
let ``kind is FileEntry for a file`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    let kind = FileSystemPath.kind fs (FileSystemPath.create (at "data/notes.txt"))

    Assert.Equal(FileEntry, kind)

[<Fact>]
let ``kind is DirectoryEntry for a directory`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")

    let kind = FileSystemPath.kind fs (FileSystemPath.create (at "data/docs"))

    Assert.Equal(DirectoryEntry, kind)

[<Fact>]
let ``linkTarget answers what a directory link points at`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    fs.Directory.CreateSymbolicLink(at "data/twin", at "data/docs") |> ignore

    let target = FileSystemPath.linkTarget fs (FileSystemPath.create (at "data/twin"))

    Assert.Equal(Some(FileSystemPath.create (at "data/docs")), target)

[<Fact>]
let ``linkTarget answers what a file link points at`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")
    fs.File.CreateSymbolicLink(at "data/twin.txt", at "data/notes.txt") |> ignore

    let target = FileSystemPath.linkTarget fs (FileSystemPath.create (at "data/twin.txt"))

    Assert.Equal(Some(FileSystemPath.create (at "data/notes.txt")), target)

[<Theory>]
[<InlineData("data/docs")>]
[<InlineData("data/notes.txt")>]
[<InlineData("data/gone")>]
let ``linkTarget answers nothing for what is no link`` path =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    let target = FileSystemPath.linkTarget fs (FileSystemPath.create (at path))

    Assert.Equal(None, target)
