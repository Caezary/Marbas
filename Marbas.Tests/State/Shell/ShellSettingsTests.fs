module Marbas.Tests.State.Shell.ShellSettingsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``changed programs are dispatched back`` () =
    let sent, dispatch = capture ()

    let changed =
        { ProgramSettings.none with
            DefaultEditor = ProgramPath.tryCreate "code.exe" }

    let deps =
        { noDeps () with
            Prompts.EditPrograms = fun _ -> Some changed }

    Shell.interpret deps dispatch (ShowSettings(ProgramSettings.none, None))

    Assert.Equal<Message list>([ ProgramsEntered changed ], List.ofSeq sent)

[<Fact>]
let ``the executable the effect names is offered to the settings popup`` () =
    let offered = ResizeArray<ProgramPath option>()

    let deps =
        { noDeps () with
            Prompts.EditPrograms =
                fun request ->
                    offered.Add request.Candidate
                    None }

    let candidate = ProgramPath.tryCreate (at "start/rider64.exe")

    Shell.interpret deps ignore (ShowSettings(ProgramSettings.none, candidate))

    Assert.Equal(candidate, Assert.Single offered)

[<Fact>]
let ``a dismissed settings dialog dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (ShowSettings(ProgramSettings.none, None))

    Assert.Empty sent

[<Fact>]
let ``saved settings are written where the settings path points`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    let settings = Settings.forPath (fp (at "start"))

    let deps =
        { noDeps () with
            FileSystem = fs
            SettingsPath = fp (at "config/marbas.conf") }

    Shell.interpret deps ignore (SaveSettings settings)

    Assert.Equal(settings, Settings.load fs settings (fp (at "config/marbas.conf")))

[<Fact>]
let ``a session that could not be written is reported`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddFile(at "blocked", MockFileData "x")
    let mutable reported = ("", "")

    let deps =
        { noDeps () with
            FileSystem = fs
            SettingsPath = fp (at "blocked/marbas.conf")
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    Shell.interpret deps ignore (SaveSettings(Settings.forPath (fp (at "start"))))

    Assert.Equal(SettingsErrorMessage.Title, fst reported)
    Assert.Contains(at "blocked/marbas.conf", snd reported)
