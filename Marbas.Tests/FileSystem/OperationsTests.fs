module Marbas.Tests.FileSystem.OperationsTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private failedPath error =
    match error with
    | OperationFailed(path, _) -> Some path
    | _ -> None

[<Fact>]
let ``makeDirectory creates a directory in the given parent`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data")

    makeDirectory fs (fp (at "data")) (en "new") |> Expect.ok

    Assert.True(fs.Directory.Exists(at "data/new"))

[<Fact>]
let ``makeDirectory refuses an existing name`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/new")

    let failure = makeDirectory fs (fp (at "data")) (en "new") |> Expect.error

    Assert.Equal(TargetExists(fp (at "data/new")), failure)

[<Fact>]
let ``makeFile creates an empty file in the given parent`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data")

    let path = makeFile fs (fp (at "data")) (en "notes.txt") |> Expect.ok

    Assert.Equal(at "data/notes.txt", FileSystemPath.value path)
    Assert.Equal("", fs.File.ReadAllText(at "data/notes.txt"))

[<Fact>]
let ``makeFile leaves an existing file as it stands`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    let path = makeFile fs (fp (at "data")) (en "notes.txt") |> Expect.ok

    Assert.Equal(at "data/notes.txt", FileSystemPath.value path)
    Assert.Equal("hello", fs.File.ReadAllText(at "data/notes.txt"))

[<Fact>]
let ``makeFile refuses a name a directory already carries`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/notes.txt")

    let failure = makeFile fs (fp (at "data")) (en "notes.txt") |> Expect.error

    Assert.Equal(TargetExists(fp (at "data/notes.txt")), failure)

[<Fact>]
let ``delete removes a file`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    delete fs unattended (fp (at "data/notes.txt")) |> Expect.ok

    Assert.False(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``delete removes a directory with its contents`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/deep/inner.txt", MockFileData "x")

    delete fs unattended (fp (at "data/docs")) |> Expect.ok

    Assert.False(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``delete reports a missing source`` () =
    let fs = MockFileSystem()

    let failure = delete fs unattended (fp (at "data/gone.txt")) |> Expect.error

    Assert.Equal(SourceNotFound(fp (at "data/gone.txt")), failure)

[<Fact>]
let ``delete reports a file the file system will not let go`` () =
    let fs = MockFileSystem()
    let guarded = MockFileData "hello"
    guarded.Attributes <- IO.FileAttributes.ReadOnly
    fs.AddFile(at "data/notes.txt", guarded)

    let failure = delete fs unattended (fp (at "data/notes.txt")) |> Expect.error

    Assert.Equal(AccessDenied(fp (at "data/notes.txt")), failure)
    Assert.True(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``delete reports a file that is in use as a failure of its own`` () =
    let fs = MockFileSystem()
    let held = MockFileData "hello"
    held.AllowedFileShare <- IO.FileShare.None
    fs.AddFile(at "data/notes.txt", held)

    let failure = delete fs unattended (fp (at "data/notes.txt")) |> Expect.error

    Assert.Equal(Some(fp (at "data/notes.txt")), failedPath failure)

[<Fact>]
let ``copy places a file in the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")
    fs.AddDirectory(at "backup")

    copy fs unattended (fp (at "backup")) (fp (at "data/notes.txt")) |> Expect.ok

    Assert.Equal("hello", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.True(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``copy replaces whatever is already there`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "new")
    fs.AddFile(at "backup/notes.txt", MockFileData "old")

    copy fs unattended (fp (at "backup")) (fp (at "data/notes.txt")) |> Expect.ok

    Assert.Equal("new", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``copy duplicates a whole directory tree`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/deep/inner.txt", MockFileData "x")
    fs.AddFile(at "data/docs/top.txt", MockFileData "y")
    fs.AddDirectory(at "backup")

    copy fs unattended (fp (at "backup")) (fp (at "data/docs")) |> Expect.ok

    Assert.Equal("x", fs.File.ReadAllText(at "backup/docs/deep/inner.txt"))
    Assert.Equal("y", fs.File.ReadAllText(at "backup/docs/top.txt"))

[<Fact>]
let ``copy reports a missing source`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")

    let failure =
        copy fs unattended (fp (at "backup")) (fp (at "data/gone.txt")) |> Expect.error

    Assert.Equal(SourceNotFound(fp (at "data/gone.txt")), failure)

[<Fact>]
let ``move takes a file to the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")
    fs.AddDirectory(at "backup")

    move fs unattended (fp (at "backup")) (fp (at "data/notes.txt")) |> Expect.ok

    Assert.Equal("hello", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.False(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``move takes a directory tree to the target directory`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.AddDirectory(at "backup")

    move fs unattended (fp (at "backup")) (fp (at "data/docs")) |> Expect.ok

    Assert.Equal("x", fs.File.ReadAllText(at "backup/docs/inner.txt"))
    Assert.False(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``copying a directory copies every file under it`` () =
    let fs = Fakes.CopyCounting()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.AddFile(at "data/docs/deep/leaf.txt", MockFileData "y")
    fs.AddDirectory(at "backup")

    let copied = copy fs unattended (fp (at "backup")) (fp (at "data/docs"))

    Expect.ok copied
    Assert.Equal(2, fs.Copies)

[<Fact>]
let ``moving a directory where the file system can rename it copies nothing`` () =
    let fs = Fakes.CopyCounting()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.AddDirectory(at "backup")

    let moved = move fs unattended (fp (at "backup")) (fp (at "data/docs"))

    Expect.ok moved
    Assert.Equal(0, fs.Copies)
    Assert.Equal("x", fs.File.ReadAllText(at "backup/docs/inner.txt"))
    Assert.False(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``moving a directory onto one that already stands merges into it`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.AddFile(at "backup/docs/other.txt", MockFileData "y")

    let moved = move fs unattended (fp (at "backup")) (fp (at "data/docs"))

    Expect.ok moved
    Assert.Equal("x", fs.File.ReadAllText(at "backup/docs/inner.txt"))
    Assert.Equal("y", fs.File.ReadAllText(at "backup/docs/other.txt"))
    Assert.False(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``a directory the file system refuses to rename is copied and then removed`` () =
    let fs = Fakes.DirectoryMoveRefused()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.AddDirectory(at "backup")

    let moved = move fs unattended (fp (at "backup")) (fp (at "data/docs"))

    Expect.ok moved
    Assert.Equal("x", fs.File.ReadAllText(at "backup/docs/inner.txt"))
    Assert.False(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``move replaces whatever is already there`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "new")
    fs.AddFile(at "backup/notes.txt", MockFileData "old")

    move fs unattended (fp (at "backup")) (fp (at "data/notes.txt")) |> Expect.ok

    Assert.Equal("new", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.False(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``a renamed file keeps the entry in its own directory and asks for no copy`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")

    let step = startRename fs (fp (at "data/notes.txt")) (en "readme.txt") |> Expect.ok

    Assert.Equal(RenameDone, step)
    Assert.Equal("hello", fs.File.ReadAllText(at "data/readme.txt"))
    Assert.False(fs.File.Exists(at "data/notes.txt"))

[<Fact>]
let ``a renamed directory the file system can move asks for no copy`` () =
    let fs = Fakes.CopyCounting()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")

    let step = startRename fs (fp (at "data/docs")) (en "papers") |> Expect.ok

    Assert.Equal(RenameDone, step)
    Assert.Equal(0, fs.Copies)
    Assert.Equal("x", fs.File.ReadAllText(at "data/papers/inner.txt"))

[<Fact>]
let ``a renamed directory the file system refuses to move asks for a copy`` () =
    let fs = Fakes.DirectoryMoveRefused()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")

    let step = startRename fs (fp (at "data/docs")) (en "papers") |> Expect.ok

    Assert.Equal(CopyNeeded(fp (at "data/papers")), step)
    Assert.True(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``a rename carried out by copying leaves the source behind it`` () =
    let fs = Fakes.DirectoryMoveRefused()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")

    moveByCopying fs unattended (fp (at "data/docs")) (fp (at "data/papers"))
    |> Expect.ok

    Assert.Equal("x", fs.File.ReadAllText(at "data/papers/inner.txt"))
    Assert.False(fs.Directory.Exists(at "data/docs"))

[<Fact>]
let ``a rename refuses an existing name`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/notes.txt", MockFileData "hello")
    fs.AddFile(at "data/readme.txt", MockFileData "other")

    let failure =
        startRename fs (fp (at "data/notes.txt")) (en "readme.txt") |> Expect.error

    Assert.Equal(TargetExists(fp (at "data/readme.txt")), failure)

[<Fact>]
let ``a rename reports a missing source`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data")

    let failure =
        startRename fs (fp (at "data/gone.txt")) (en "readme.txt") |> Expect.error

    Assert.Equal(SourceNotFound(fp (at "data/gone.txt")), failure)

[<Fact>]
let ``a directory holding entries is non-empty`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")

    let nonEmpty = isNonEmptyDirectory fs (fp (at "data/docs"))

    Assert.True nonEmpty

[<Fact>]
let ``an empty directory and a file are not non-empty directories`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "data/empty")
    fs.AddFile(at "data/notes.txt", MockFileData "x")

    let emptyDirectory = isNonEmptyDirectory fs (fp (at "data/empty"))
    let file = isNonEmptyDirectory fs (fp (at "data/notes.txt"))

    Assert.False emptyDirectory
    Assert.False file

[<Fact>]
let ``a directory link is not a non-empty directory`` () =
    let fs = Fakes.LinkFollowing()
    fs.AddFile(at "data/docs/inner.txt", MockFileData "x")
    fs.Directory.CreateSymbolicLink(at "data/twin", at "data/docs") |> ignore

    let nonEmpty = isNonEmptyDirectory fs (fp (at "data/twin"))

    Assert.False nonEmpty
