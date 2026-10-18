module Marbas.Tests.Adapters.LaunchProcessTests

open System.ComponentModel
open System.IO
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Programs
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
            DrainedPrograms.none
            { elevating (at "media/stick") with
                Elevation = AsUser }

    let start = Expect.ok described
    Assert.Equal(LaunchProcess.DesktopOpener, start.FileName)
    Assert.Equal<string list>([ at "media/stick" ], List.ofSeq start.ArgumentList)

let private moded (mode: UnixFileMode) =
    let data = MockFileData "#!/bin/sh"
    data.UnixMode <- mode
    data

let private asUser target =
    { elevating target with
        Elevation = AsUser }

[<Fact>]
let ``a file with its executable bit set is started from its own file on Unix`` () =
    let fs = Fakes.ModeWitness()

    fs.AddFile(at "start/tool", moded (UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute))

    let described =
        LaunchProcess.describe fs Unix DrainedPrograms.none (asUser (at "start/tool"))

    let start = Expect.ok described
    Assert.Equal(at "start/tool", start.FileName)
    Assert.Empty start.ArgumentList
    Assert.Equal<string list>([ at "start/tool" ], fs.AskedForMode)

[<Fact>]
let ``a file without the bit is handed to the desktop's opener on Unix`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes", moded (UnixFileMode.UserRead ||| UnixFileMode.UserWrite))

    let described =
        LaunchProcess.describe fs Unix DrainedPrograms.none (asUser (at "start/notes"))

    let start = Expect.ok described
    Assert.Equal(LaunchProcess.DesktopOpener, start.FileName)
    Assert.Equal<string list>([ at "start/notes" ], List.ofSeq start.ArgumentList)

let private notThere = Win32Exception 2

let private describedOnUnix (fs: MockFileSystem) request =
    LaunchProcess.describe fs Unix DrainedPrograms.none request |> Expect.ok

let private desktopOpener =
    Expect.some (ProgramPath.tryCreate LaunchProcess.DesktopOpener)

[<Fact>]
let ``a document whose opener is not installed is reported as an opener missing`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes", moded (UnixFileMode.UserRead ||| UnixFileMode.UserWrite))
    let request = asUser (at "start/notes")

    let failure = LaunchProcess.failure (describedOnUnix fs request) request notThere

    Assert.Equal(OpenerNotInstalled desktopOpener, failure)

[<Fact>]
let ``a directory whose opener is not installed is reported as an opener missing`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "media/stick")
    let request = asUser (at "media/stick")

    let failure = LaunchProcess.failure (describedOnUnix fs request) request notThere

    Assert.Equal(OpenerNotInstalled desktopOpener, failure)

[<Fact>]
let ``a program started from its own file that is not there is not an opener missing`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/tool", moded (UnixFileMode.UserRead ||| UnixFileMode.UserExecute))
    let request = asUser (at "start/tool")

    let failure = LaunchProcess.failure (describedOnUnix fs request) request notThere

    Assert.Equal(LaunchRefused(fp (at "start/tool"), notThere.Message), failure)

[<Fact>]
let ``a chosen program that is not there is still named as the program`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes", MockFileData "")
    let tool = Expect.some (ProgramPath.tryCreate "tool")

    let request =
        { asUser (at "start/notes") with
            Program = Some tool }

    let failure = LaunchProcess.failure (describedOnUnix fs request) request notThere

    Assert.Equal(ProgramNotFound tool, failure)

[<Fact>]
let ``a file whose mode cannot be read on Unix is reported, not started`` () =
    let fs = Fakes.ModeWitness [ at "start/gone" ]
    fs.AddFile(at "start/gone", MockFileData "")

    let outcome =
        LaunchProcess.start fs Unix DrainedPrograms.none (asUser (at "start/gone"))

    Assert.Equal(Error(LaunchRefused(fp (at "start/gone"), at "start/gone")), outcome)

[<Fact>]
let ``a Unix system has no administrator rights to ask for, and says so`` () =
    let outcome =
        LaunchProcess.start (MockFileSystem()) Unix DrainedPrograms.none (elevating (at "start/tool"))

    Assert.Equal(Error(LaunchRefused(fp (at "start/tool"), LaunchProcess.ElevationUnavailable)), outcome)

let private opening target program =
    { Target = fp target
      Program = program
      WorkingDirectory = fp (at "start")
      Elevation = AsUser }

let private codeKnown = DrainedPrograms.windowImage "code.exe" (at "apps/Code.exe")

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
        DrainedPrograms.windowImage (at "start/viewer.exe") (at "start/viewer.exe")

    let described =
        LaunchProcess.describe (MockFileSystem()) Windows windowPrograms (opening (at "start/viewer.exe") None)

    let start = Expect.ok described
    Assert.Equal(at "start/viewer.exe", start.FileName)
    Assert.Empty start.ArgumentList
    Assert.False start.UseShellExecute
    Assert.True start.RedirectStandardOutput

[<Fact>]
let ``a batch script chosen for a file on Windows runs through cmd without a console window, its streams drained`` () =
    let script = at "apps/code.cmd"
    let notes = at "start/notes.txt"
    let codeScript = DrainedPrograms.batchScript "code" script

    let described =
        LaunchProcess.describe (MockFileSystem()) Windows codeScript (opening notes (ProgramPath.tryCreate "code"))

    let start = Expect.ok described
    Assert.Equal(LaunchProcess.WindowsShell, start.FileName)
    Assert.Equal($"/d /s /c \"\"{script}\" \"{notes}\"\"", start.Arguments)
    Assert.False start.UseShellExecute
    Assert.True start.RedirectStandardOutput
    Assert.True start.RedirectStandardError
    Assert.True start.CreateNoWindow

[<Fact>]
let ``a batch script opened itself keeps the shell verb on Windows, for a console of its own`` () =
    let script = at "start/build.cmd"
    let buildScript = DrainedPrograms.batchScript script script

    let described =
        LaunchProcess.describe (MockFileSystem()) Windows buildScript (opening script None)

    let start = Expect.ok described
    Assert.Equal(script, start.FileName)
    Assert.True start.UseShellExecute

[<Fact>]
let ``a batch script started hidden has its input closed, so that nothing it asks for waits unseen`` () =
    let script = LaunchProcess.BatchScript(fp @"C:\tools\ask.cmd")

    let start = LaunchProcess.detachedFrom script []

    Assert.True start.RedirectStandardInput

[<Fact>]
let ``every word handed to a batch script is quoted for cmd, a trailing backslash doubled`` () =
    let script = LaunchProcess.BatchScript(fp @"C:\Program Files\VS Code\bin\code.cmd")

    let start = LaunchProcess.detachedFrom script [ @"C:\R&D\notes.txt"; @"C:\" ]

    Assert.Equal(
        "/d /s /c \"\"C:\\Program Files\\VS Code\\bin\\code.cmd\" \"C:\\R&D\\notes.txt\" \"C:\\\\\"\"",
        start.Arguments
    )

[<Fact>]
let ``a program that is not a window program keeps the shell verb on Windows, for a console of its own`` () =
    let described =
        LaunchProcess.describe
            (MockFileSystem())
            Windows
            DrainedPrograms.none
            (opening (at "start/notes.txt") (ProgramPath.tryCreate "tool.exe"))

    let start = Expect.ok described
    Assert.Equal("tool.exe", start.FileName)
    Assert.True start.UseShellExecute

[<Fact>]
let ``a file opened through its association keeps the shell verb on Windows`` () =
    let described =
        LaunchProcess.describe (MockFileSystem()) Windows DrainedPrograms.none (opening (at "start/notes.txt") None)

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
