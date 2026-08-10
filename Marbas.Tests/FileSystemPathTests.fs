module Marbas.Tests.FileSystemPathTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem

[<Fact>]
let ``value returns the wrapped string`` () =
    let dataPath = FileSystemPath.create @"C:\data"

    let value = FileSystemPath.value dataPath

    Assert.Equal(@"C:\data", value)

[<Fact>]
let ``equal paths compare equal`` () =
    let dataPath = FileSystemPath.create @"C:\data"

    let samePath = FileSystemPath.create @"C:\data"

    Assert.Equal(dataPath, samePath)

[<Fact>]
let ``combine appends a child name`` () =
    let fs = MockFileSystem()
    let parentFolder = FileSystemPath.create @"C:\data"

    let combined = FileSystemPath.combine fs parentFolder "child"

    Assert.Equal(@"C:\data\child", FileSystemPath.value combined)

[<Fact>]
let ``name returns the last segment in a file path - its name`` () =
    let fs = MockFileSystem()
    let notesPath = FileSystemPath.create @"C:\data\notes.txt"

    let name = FileSystemPath.name fs notesPath

    Assert.Equal("notes.txt", name)

[<Fact>]
let ``name returns the last segment in a directory path - its name`` () =
    let fs = MockFileSystem()
    let childPath = FileSystemPath.create @"C:\data\child"

    let name = FileSystemPath.name fs childPath

    Assert.Equal("child", name)

[<Fact>]
let ``directory returns the containing path`` () =
    let fs = MockFileSystem()
    let notesPath = FileSystemPath.create @"C:\data\notes.txt"

    let containing = FileSystemPath.directory fs notesPath

    Assert.Equal(@"C:\data", FileSystemPath.value containing)

[<Fact>]
let ``parent returns the enclosing directory`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data\docs"
    let docsPath = FileSystemPath.create @"C:\data\docs"

    let parent = FileSystemPath.parent fs docsPath

    Assert.Equal(Some @"C:\data", parent |> Option.map FileSystemPath.value)

[<Fact>]
let ``parent of a root is None`` () =
    let fs = MockFileSystem()
    let rootPath = FileSystemPath.create @"C:\"

    let parent = FileSystemPath.parent fs rootPath

    Assert.Equal(None, parent)

[<Fact>]
let ``a name without a directory part is a bare name`` () =
    let fs = MockFileSystem()

    let isBare = FileSystemPath.isBareName fs "notes.txt"

    Assert.True isBare

[<Fact>]
let ``a name carrying a directory is not a bare name`` () =
    let fs = MockFileSystem()

    let isBare = FileSystemPath.isBareName fs @"docs\notes.txt"

    Assert.False isBare

[<Fact>]
let ``a rooted path is not a bare name`` () =
    let fs = MockFileSystem()

    let backupIsBare = FileSystemPath.isBareName fs @"C:\backup"
    let rootIsBare = FileSystemPath.isBareName fs @"C:\"

    Assert.False backupIsBare
    Assert.False rootIsBare

[<Fact>]
let ``kind is NoEntry for a missing path`` () =
    let fs = MockFileSystem()

    let kind = FileSystemPath.kind fs (FileSystemPath.create @"C:\data\missing.txt")

    Assert.Equal(NoEntry, kind)

[<Fact>]
let ``kind is FileEntry for a file`` () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\data\notes.txt", MockFileData "hello")

    let kind = FileSystemPath.kind fs (FileSystemPath.create @"C:\data\notes.txt")

    Assert.Equal(FileEntry, kind)

[<Fact>]
let ``kind is DirectoryEntry for a directory`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\data\docs"

    let kind = FileSystemPath.kind fs (FileSystemPath.create @"C:\data\docs")

    Assert.Equal(DirectoryEntry, kind)
