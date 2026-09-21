module Marbas.Tests.State.PropertiesTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs

let private loaded (fs: MockFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "start"))
    |> Expect.ok

[<Fact>]
let ``the directory under the cursor is the one whose properties are asked for`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1

    Assert.Equal(Some(fp (at "start/docs")), cursorDirectoryPath fs state.Left)

[<Fact>]
let ``a file under the cursor is no directory to show properties for`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    Assert.Equal(None, cursorDirectoryPath fs state.Left)

[<Fact>]
let ``the parent entry has no properties of its own`` () =
    let fs = mockFs ()

    let state =
        create (fp (at "start"))
        |> loadPanel fs Left (fp (at "start/docs"))
        |> Expect.ok
        |> setCursor Left 0

    Assert.Equal(None, cursorDirectoryPath fs state.Left)

[<Fact>]
let ``properties that are applied reach the entry`` () =
    let fs = mockFs ()
    let state = loaded fs
    let path = fp (at "start/docs")

    let properties =
        EntryProperties.read fs path |> Expect.ok |> EntryProperties.setFlag HiddenFlag true

    applyProperties fs properties state |> Expect.ok |> ignore

    Assert.True(EntryProperties.read fs path |> Expect.ok |> EntryProperties.has HiddenFlag)

[<Fact>]
let ``a directory that has just been hidden leaves a panel that conceals it`` () =
    let fs = mockFs ()
    let state = loaded fs

    let properties =
        EntryProperties.read fs (fp (at "start/docs"))
        |> Expect.ok
        |> EntryProperties.setFlag HiddenFlag true

    let applied = applyProperties fs properties state |> Expect.ok

    Assert.DoesNotContain(EntryName.create "docs", applied.Left.Entries |> Array.map _.Name)

[<Fact>]
let ``a stamp that is applied is what the entry carries afterwards`` () =
    let fs = mockFs ()
    let state = loaded fs
    let path = fp (at "start/docs")
    let created = DateTimeOffset(DateTime(2001, 2, 3, 4, 5, 6))

    let read = EntryProperties.read fs path |> Expect.ok
    let properties = { read with Created = created }

    applyProperties fs properties state |> Expect.ok |> ignore

    Assert.Equal(created, (EntryProperties.read fs path |> Expect.ok).Created)

[<Fact>]
let ``properties of an entry that is gone are reported`` () =
    let fs = mockFs ()
    let state = loaded fs
    let properties = EntryProperties.read fs (fp (at "start/docs")) |> Expect.ok
    fs.Directory.Delete(at "start/docs")

    let failure = applyProperties fs properties state |> Expect.error

    Assert.Equal(Operations.SourceNotFound(fp (at "start/docs")), failure)
