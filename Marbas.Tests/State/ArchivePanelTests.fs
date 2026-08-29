module Marbas.Tests.State.ArchivePanelTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private archive = fp (at "start/backup.zip")

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private standingInside (fs: IFileSystem) side =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok
    |> fun state ->
        let inside (p: PanelState) =
            { p with Path = InArchive(archive, ArchivePath.root) }

        match side with
        | Left -> { state with Left = inside state.Left }
        | Right -> { state with Right = inside state.Right }

[<Fact>]
let ``the targets of a panel inside an archive have no paths on disk`` () =
    let fs = mockFs ()
    let state = standingInside fs Left

    let paths = operationPaths fs (activePanel state)

    Assert.Empty paths

[<Fact>]
let ``a copy out of a panel inside an archive asks for nothing`` () =
    let fs = mockFs ()
    let state = standingInside fs Left

    let result = update fs CopyRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``a copy into a panel inside an archive asks for nothing`` () =
    let fs = mockFs ()
    let state = standingInside fs Right |> setCursor Left 1

    let result = update fs CopyRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``a delete inside an archive asks nothing to be confirmed`` () =
    let fs = mockFs ()
    let state = standingInside fs Left |> setCursor Left 1

    let result = update fs (DeleteRequested ToTrash) state

    Assert.Empty result.Effects

[<Fact>]
let ``a directory is not made inside an archive`` () =
    let fs = mockFs ()
    let state = standingInside fs Left

    let result = update fs MakeDirectoryRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``a file is not made inside an archive`` () =
    let fs = mockFs ()
    let state = standingInside fs Left

    let result = update fs NewFileRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``packing from a panel inside an archive asks for nothing`` () =
    let fs = mockFs ()
    let state = standingInside fs Left

    let result = update fs PackRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``a refresh of a panel inside an archive leaves it as it stands`` () =
    let fs = mockFs ()
    let state = standingInside fs Left

    let result = update fs RefreshRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``an entry opened inside an archive leaves the panel as it stands`` () =
    let fs = mockFs ()
    let state = standingInside fs Left

    let result = update fs EntryOpened state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects
