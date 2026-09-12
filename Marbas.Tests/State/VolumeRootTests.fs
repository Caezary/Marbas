module Marbas.Tests.State.VolumeRootTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs/inner")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private standingOn (fs: IFileSystem) path =
    create (fp (at "start"))
    |> loadPanel fs Left (fp path)
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok

[<Fact>]
let ``a panel deep inside a volume is sent to its root`` () =
    let fs = mockFs ()
    let state = standingOn fs (at "start/docs/inner")

    let result = update fs VolumeRootRequested state

    Assert.Equal(OnDisk(fp (at "")), result.State.Left.Path)

[<Fact>]
let ``the cursor lands at the top of the root`` () =
    let fs = mockFs ()
    let state = standingOn fs (at "start/docs") |> setCursor Left 1

    let result = update fs VolumeRootRequested state

    Assert.Equal(0, result.State.Left.Cursor)

[<Fact>]
let ``the panel that is not active stays where it stands`` () =
    let fs = mockFs ()
    let state = standingOn fs (at "start/docs")

    let result = update fs VolumeRootRequested state

    Assert.Equal(OnDisk(fp (at "backup")), result.State.Right.Path)

[<Fact>]
let ``the active panel is the one sent, whichever side it is`` () =
    let fs = mockFs ()
    let state = standingOn fs (at "start/docs") |> activate Right

    let result = update fs VolumeRootRequested state

    Assert.Equal(OnDisk(fp (at "")), result.State.Right.Path)
    Assert.Equal(OnDisk(fp (at "start/docs")), result.State.Left.Path)

[<Fact>]
let ``a panel already standing on its root has nothing to do`` () =
    let fs = mockFs ()
    let state = standingOn fs (at "") |> setCursor Left 1

    let result = update fs VolumeRootRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a panel inside an archive leaves it for the root of the volume`` () =
    let fs = packed ()
    let state = standingInside fs Left

    let result = update fs VolumeRootRequested state

    Assert.Equal(OnDisk(fp (at "")), result.State.Left.Path)

[<WindowsFact>]
let ``the root is the one of the volume the panel stands on`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"D:\data\deep"
    let state = @"D:\data"
                |> fp
                |> create
                |> loadPanel fs Left (fp @"D:\data\deep")
                |> Expect.ok

    let result = update fs VolumeRootRequested state

    Assert.Equal(OnDisk(fp @"D:\"), result.State.Left.Path)
