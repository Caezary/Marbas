module Marbas.Tests.Adapters.LaunchProcessTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Adapters
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private elevating target =
    { Target = fp target
      Program = None
      WorkingDirectory = fp (at "start")
      Elevation = AsAdministrator }

[<Fact>]
let ``a directory on Unix is handed to the desktop's opener rather than run`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "media/stick")

    let described =
        LaunchProcess.describe
            fs
            Unix
            { elevating (at "media/stick") with
                Elevation = AsUser }

    let start = Expect.ok described
    Assert.Equal(LaunchProcess.DesktopOpener, start.FileName)
    Assert.Equal<string list>([ at "media/stick" ], List.ofSeq start.ArgumentList)

[<Fact>]
let ``a Unix system has no administrator rights to ask for, and says so`` () =
    let outcome =
        LaunchProcess.start (MockFileSystem()) Unix (elevating (at "start/tool"))

    Assert.Equal(Error(LaunchRefused(fp (at "start/tool"), LaunchProcess.ElevationUnavailable)), outcome)
