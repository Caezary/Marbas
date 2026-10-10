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
            WindowPrograms.none
            { elevating (at "media/stick") with
                Elevation = AsUser }

    let start = Expect.ok described
    Assert.Equal(LaunchProcess.DesktopOpener, start.FileName)
    Assert.Equal<string list>([ at "media/stick" ], List.ofSeq start.ArgumentList)

[<Fact>]
let ``a Unix system has no administrator rights to ask for, and says so`` () =
    let outcome =
        LaunchProcess.start (MockFileSystem()) Unix WindowPrograms.none (elevating (at "start/tool"))

    Assert.Equal(Error(LaunchRefused(fp (at "start/tool"), LaunchProcess.ElevationUnavailable)), outcome)

let private opening target program =
    { Target = fp target
      Program = program
      WorkingDirectory = fp (at "start")
      Elevation = AsUser }

let private codeKnown = WindowPrograms.knowing "code.exe" (at "apps/Code.exe")

[<Fact>]
let ``a window program chosen for a file on Windows is started from its file with its streams drained`` () =
    let described =
        LaunchProcess.describe
            (MockFileSystem())
            Windows
            codeKnown
            (opening (at "start/notes.txt") (ProgramPath.tryCreate "code.exe"))

    let start = Expect.ok described
    Assert.Equal(at "apps/Code.exe", start.FileName)
    Assert.Equal<string list>([ at "start/notes.txt" ], List.ofSeq start.ArgumentList)
    Assert.False start.UseShellExecute
    Assert.True start.RedirectStandardOutput

[<Fact>]
let ``an executable that is a window program is started from its file on Windows`` () =
    let windowPrograms =
        WindowPrograms.knowing (at "start/viewer.exe") (at "start/viewer.exe")

    let described =
        LaunchProcess.describe (MockFileSystem()) Windows windowPrograms (opening (at "start/viewer.exe") None)

    let start = Expect.ok described
    Assert.Equal(at "start/viewer.exe", start.FileName)
    Assert.Empty start.ArgumentList
    Assert.False start.UseShellExecute
    Assert.True start.RedirectStandardOutput

[<Fact>]
let ``a program that is not a window program keeps the shell verb on Windows, for a console of its own`` () =
    let described =
        LaunchProcess.describe
            (MockFileSystem())
            Windows
            WindowPrograms.none
            (opening (at "start/notes.txt") (ProgramPath.tryCreate "tool.exe"))

    let start = Expect.ok described
    Assert.Equal("tool.exe", start.FileName)
    Assert.True start.UseShellExecute

[<Fact>]
let ``a file opened through its association keeps the shell verb on Windows`` () =
    let described =
        LaunchProcess.describe (MockFileSystem()) Windows WindowPrograms.none (opening (at "start/notes.txt") None)

    let start = Expect.ok described
    Assert.Equal(at "start/notes.txt", start.FileName)
    Assert.True start.UseShellExecute

[<Fact>]
let ``a window program run as administrator on Windows keeps the shell verb, the only way to ask`` () =
    let described =
        LaunchProcess.describe
            (MockFileSystem())
            Windows
            codeKnown
            { opening (at "start/notes.txt") (ProgramPath.tryCreate "code.exe") with
                Elevation = AsAdministrator }

    let start = Expect.ok described
    Assert.True start.UseShellExecute
    Assert.Equal(LaunchProcess.ElevatedVerb, start.Verb)
