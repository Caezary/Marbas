module Marbas.Tests.FileSystem.TrashTests

open System
open System.IO
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private trash = fp (at "home/.local/share/Trash")

let private deletedAt = DateTimeOffset(DateTime(2026, 8, 23, 14, 5, 9))

let private clock () = deletedAt

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    fs.AddDirectory(at "home")
    fs

let private send fs source = Trash.send fs trash clock (fp (at source))

let private inTrash directory name = at $"home/.local/share/Trash/{directory}/{name}"

[<Fact>]
let ``the data home holds the trash when it is set`` () =
    let fs = mockFs ()

    let location = Trash.location fs (at "data") (at "home")

    Assert.Equal(fp (at "data/Trash"), location)

[<Fact>]
let ``without a data home the trash sits under the user profile`` () =
    let fs = mockFs ()

    let location = Trash.location fs "" (at "home")

    Assert.Equal(fp (at "home/.local/share/Trash"), location)

[<Fact>]
let ``a file is moved into the trash`` () =
    let fs = mockFs ()

    send fs "start/notes.txt" |> Expect.ok

    Assert.False(fs.File.Exists(at "start/notes.txt"))
    Assert.Equal("hi", fs.File.ReadAllText(inTrash Trash.FilesDirectory "notes.txt"))

[<Fact>]
let ``where the entry came from is recorded beside it`` () =
    let fs = mockFs ()

    send fs "start/notes.txt" |> Expect.ok

    let recorded = fs.File.ReadAllText(inTrash Trash.InfoDirectory "notes.txt.trashinfo")

    Assert.StartsWith(Trash.InfoHeader, recorded)
    Assert.Contains("notes.txt", recorded)
    Assert.Contains("DeletionDate=2026-08-23T14:05:09", recorded)

[<Fact>]
let ``a name the file system escapes is recorded encoded`` () =
    let fs = mockFs ()
    fs.AddFile(at "start/my notes.txt", MockFileData "hi")

    send fs "start/my notes.txt" |> Expect.ok

    let recorded = fs.File.ReadAllText(inTrash Trash.InfoDirectory "my notes.txt.trashinfo")

    Assert.Contains("my%20notes.txt", recorded)

[<Fact>]
let ``a directory goes to the trash with everything inside it`` () =
    let fs = mockFs ()

    send fs "start/docs" |> Expect.ok

    Assert.False(fs.Directory.Exists(at "start/docs"))
    Assert.Equal("x", fs.File.ReadAllText(inTrash Trash.FilesDirectory "docs/inner.txt"))

[<Fact>]
let ``a name already in the trash is numbered instead of overwritten`` () =
    let fs = mockFs ()
    fs.AddFile(at "other/notes.txt", MockFileData "second")
    send fs "start/notes.txt" |> Expect.ok

    send fs "other/notes.txt" |> Expect.ok

    Assert.Equal("hi", fs.File.ReadAllText(inTrash Trash.FilesDirectory "notes.txt"))
    Assert.Equal("second", fs.File.ReadAllText(inTrash Trash.FilesDirectory "notes_2.txt"))
    Assert.True(fs.File.Exists(inTrash Trash.InfoDirectory "notes_2.txt.trashinfo"))

[<Fact>]
let ``an entry that is not there is reported`` () =
    let fs = mockFs ()

    let failure = send fs "start/gone.txt" |> Expect.error

    Assert.Equal(SourceNotFound(fp (at "start/gone.txt")), failure)

[<Fact>]
let ``an entry the file system will not let go leaves nothing in the trash`` () =
    let fs = mockFs ()
    let guarded = MockFileData "hello"
    guarded.Attributes <- FileAttributes.ReadOnly
    fs.AddFile(at "start/guarded.txt", guarded)

    send fs "start/guarded.txt" |> Expect.error |> ignore

    Assert.True(fs.File.Exists(at "start/guarded.txt"))
    Assert.False(fs.File.Exists(inTrash Trash.InfoDirectory "guarded.txt.trashinfo"))
