module Marbas.Tests.FileSystem.FileSystemPathTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

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
let ``a root is its own directory`` () =
    let fs = MockFileSystem()
    let rootPath = FileSystemPath.create (at "")

    let containing = FileSystemPath.directory fs rootPath

    Assert.Equal(rootPath, containing)

[<Fact>]
let ``parent returns the enclosing directory`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/docs")
    let docsPath = FileSystemPath.create (at "data/docs")

    let parent = FileSystemPath.parent fs docsPath

    Assert.Equal(Some(at "data"), parent |> Option.map FileSystemPath.value)

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

    let nearest =
        FileSystemPath.nearest fs (FileSystemPath.create (at "data/docs/deep"))

    Assert.Equal(Some(at "data"), nearest |> Option.map FileSystemPath.value)

[<Fact>]
let ``the nearest directory to a file is the directory holding it`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hi")

    let nearest =
        FileSystemPath.nearest fs (FileSystemPath.create (at "data/notes.txt"))

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

    let target =
        FileSystemPath.linkTarget fs (FileSystemPath.create (at "data/twin.txt"))

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

[<WindowsFact>]
let ``a file system that separates with a backslash is Windows`` () =
    let fs = MockFileSystem()

    let platform = FileSystemPath.platformOf fs

    Assert.Equal<Platform>(Windows, platform)

[<UnixFact>]
let ``a file system that separates with a slash is Unix`` () =
    let fs = MockFileSystem()

    let platform = FileSystemPath.platformOf fs

    Assert.Equal<Platform>(Unix, platform)

[<Theory>]
[<InlineData(@"C:\work", @"C:\work\docs")>]
[<InlineData(@"C:\WORK", @"C:\work\docs")>]
[<InlineData(@"C:\work\", @"C:\work\docs")>]
[<InlineData(@"C:\", @"C:\work")>]
let ``on Windows a path below a root lies strictly under it whatever the case`` root path =
    let result = FileSystemPath.strictlyUnder Windows (fp root) (fp path)

    Assert.True result

[<Theory>]
[<InlineData(@"C:\work", @"C:\work")>]
[<InlineData(@"C:\work", @"C:\workshop")>]
[<InlineData(@"C:\work", @"C:\other\docs")>]
let ``on Windows a path that is the root or beside it lies under nothing`` root path =
    let result = FileSystemPath.strictlyUnder Windows (fp root) (fp path)

    Assert.False result

[<Theory>]
[<InlineData("/work", "/work/docs")>]
[<InlineData("/work/", "/work/docs")>]
[<InlineData("/", "/work")>]
let ``on Unix a path below a root in the same case lies strictly under it`` root path =
    let result = FileSystemPath.strictlyUnder Unix (fp root) (fp path)

    Assert.True result

[<Theory>]
[<InlineData("/WORK", "/work/docs")>]
[<InlineData("/work", "/work")>]
[<InlineData("/work", "/workshop")>]
[<InlineData("/work", "/other/docs")>]
let ``on Unix a path in another case or beside the root lies under nothing`` root path =
    let result = FileSystemPath.strictlyUnder Unix (fp root) (fp path)

    Assert.False result

[<Theory>]
[<InlineData(@"C:\work", @"C:\work")>]
[<InlineData(@"C:\WORK", @"C:\work")>]
[<InlineData(@"C:\work", @"C:\work\docs")>]
let ``on Windows a root holds what is at it or under it whatever the case`` root path =
    let result = FileSystemPath.atOrUnder Windows (fp root) (fp path)

    Assert.True result

[<Theory>]
[<InlineData(@"C:\work", @"C:\other")>]
[<InlineData(@"C:\work", @"C:\workshop")>]
let ``on Windows a root holds nothing beside it`` root path =
    let result = FileSystemPath.atOrUnder Windows (fp root) (fp path)

    Assert.False result

[<Theory>]
[<InlineData("/work", "/work")>]
[<InlineData("/work", "/work/docs")>]
let ``on Unix a root holds what is at it or under it in the same case`` root path =
    let result = FileSystemPath.atOrUnder Unix (fp root) (fp path)

    Assert.True result

[<Theory>]
[<InlineData("/WORK", "/work")>]
[<InlineData("/work", "/other")>]
let ``on Unix a root holds nothing in another case or beside it`` root path =
    let result = FileSystemPath.atOrUnder Unix (fp root) (fp path)

    Assert.False result

[<Theory>]
[<InlineData(@"C:\work", @"C:\WORK")>]
[<InlineData(@"C:\work\", @"C:\work")>]
let ``on Windows two spellings of one path are the same entry`` one other =
    let result = FileSystemPath.sameEntry Windows (fp one) (fp other)

    Assert.True result

[<Theory>]
[<InlineData(@"C:\work", @"C:\work\docs")>]
[<InlineData(@"C:\work", @"C:\workshop")>]
let ``on Windows two different paths are not the same entry`` one other =
    let result = FileSystemPath.sameEntry Windows (fp one) (fp other)

    Assert.False result

[<Theory>]
[<InlineData("/work/", "/work")>]
[<InlineData("/work", "/work")>]
let ``on Unix two spellings of one path are the same entry`` one other =
    let result = FileSystemPath.sameEntry Unix (fp one) (fp other)

    Assert.True result

[<Theory>]
[<InlineData("/work", "/WORK")>]
[<InlineData("/work", "/work/docs")>]
let ``on Unix a path in another case is not the same entry`` one other =
    let result = FileSystemPath.sameEntry Unix (fp one) (fp other)

    Assert.False result

[<Theory>]
[<InlineData(@"C:\work", @"C:\work\docs\inner.txt", @"docs\inner.txt")>]
[<InlineData(@"C:\WORK", @"C:\work\docs", "docs")>]
[<InlineData(@"C:\work\", @"C:\work\docs", "docs")>]
[<InlineData(@"C:\", @"C:\work", "work")>]
let ``on Windows what lies under a root is spelled without it`` root path expected =
    let result = FileSystemPath.pathAfter Windows (fp root) (fp path)

    Assert.Equal(Some expected, result)

[<Theory>]
[<InlineData("/work", "/work/docs/inner.txt", "docs/inner.txt")>]
[<InlineData("/", "/work", "work")>]
let ``on Unix what lies under a root is spelled without it`` root path expected =
    let result = FileSystemPath.pathAfter Unix (fp root) (fp path)

    Assert.Equal(Some expected, result)

[<Theory>]
[<InlineData("/work", "/other/docs")>]
[<InlineData("/work", "/work")>]
[<InlineData("/WORK", "/work/docs")>]
let ``on Unix a path that is not under the root has nothing after it`` root path =
    let result = FileSystemPath.pathAfter Unix (fp root) (fp path)

    Assert.Equal(None, result)

[<Fact>]
let ``distinct entries keep the first spelling of each entry`` () =
    let platform = FileSystemPath.platformOf (MockFileSystem())

    let paths =
        [ fp (at "bin")
          fp (at "tools")
          fp (at "bin" + string System.IO.Path.DirectorySeparatorChar) ]

    let distinct = FileSystemPath.distinctEntries platform paths

    Assert.Equal<FileSystemPath list>([ fp (at "bin"); fp (at "tools") ], distinct)

[<Fact>]
let ``a relative path is resolved from the directory given, steps up included`` () =
    let fs = MockFileSystem()
    let relative = Root.relative "../work/notes.txt"

    let resolved = FileSystemPath.resolvedFrom fs (fp (at "start")) relative

    Assert.Equal(fp (at "work/notes.txt"), resolved)

[<Fact>]
let ``a full path is resolved to itself, whatever the directory`` () =
    let fs = MockFileSystem()

    let resolved =
        FileSystemPath.resolvedFrom fs (fp (at "start")) (at "work/notes.txt")

    Assert.Equal(fp (at "work/notes.txt"), resolved)
