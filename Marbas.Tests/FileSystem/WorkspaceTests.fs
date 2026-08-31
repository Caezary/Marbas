module Marbas.Tests.FileSystem.WorkspaceTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private session = "0f3c9a2b"

[<Fact>]
let ``a session works under a directory of its own inside the temporary one`` () =
    let fs = MockFileSystem()

    let path = Workspace.location fs (at "temp") session

    Assert.Equal(at $"temp/Marbas/{session}", FileSystemPath.value path)

[<Fact>]
let ``a temporary root nobody named falls back to the platform's own`` () =
    let fs = MockFileSystem()

    let path = Workspace.location fs "" session

    Assert.Equal(
        fs.Path.Combine(fs.Path.GetTempPath(), Workspace.DirectoryName, session),
        FileSystemPath.value path
    )

[<Fact>]
let ``a workspace asked for the first time is made`` () =
    let fs = MockFileSystem()
    let path = fp (at "temp/Marbas/session")

    let opened = Workspace.ensure fs path |> Expect.ok

    Assert.Equal(path, opened)
    Assert.True(fs.Directory.Exists(at "temp/Marbas/session"))

[<Fact>]
let ``a workspace already there is answered as it stands`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "temp/Marbas/session/left.txt", MockFileData "left")
    let path = fp (at "temp/Marbas/session")

    let opened = Workspace.ensure fs path |> Expect.ok

    Assert.Equal(path, opened)
    Assert.True(fs.File.Exists(at "temp/Marbas/session/left.txt"))

[<Fact>]
let ``a workspace that cannot be made is an operation failure`` () =
    let fs = Fakes.UnwritableDirectory(at "temp/Marbas/session")

    let failure = Workspace.ensure fs (fp (at "temp/Marbas/session")) |> Expect.error

    Assert.Equal(Operations.AccessDenied(fp (at "temp/Marbas/session")), failure)

[<Fact>]
let ``releasing a workspace takes everything in it`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "temp/Marbas/session/deep/left.txt", MockFileData "left")

    Workspace.release fs (fp (at "temp/Marbas/session"))

    Assert.False(fs.Directory.Exists(at "temp/Marbas/session"))

[<Fact>]
let ``releasing a workspace that was never made does nothing`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "temp")

    Workspace.release fs (fp (at "temp/Marbas/session"))

    Assert.True(fs.Directory.Exists(at "temp"))
