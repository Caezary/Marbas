module Marbas.Tests.State.UserMenuRunTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.CommandExpansion
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private listFile = fp (at "session/marked-1.txt")

let private tree () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "backup/old.txt", MockFileData "old")
    fs

let private Notes = 2

let private loaded (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok
    |> setCursor Left Notes

let private entry line =
    { Hotkey = Expect.some (MenuHotkey.tryCreate "S")
      Label = Expect.some (MenuLabel.tryCreate "Show")
      Line = Expect.some (CommandLine.tryCreate line)
      Run = Directly
      Elevation = AsUser }

let private showing: CommandRun =
    { Label = Expect.some (MenuLabel.tryCreate "Show")
      Command =
        { Program = { Text = "show"; Quoting = Bare }
          Arguments = [ { Text = at "start/notes.txt"; Quoting = Quoted } ]
          ListFile = None }
      Run = Directly
      Elevation = AsUser
      WorkingDirectory = fp (at "start") }

let private startedIn effects =
    effects
    |> List.choose (fun effect ->
        match effect with
        | RunCommand run -> Some run.WorkingDirectory
        | _ -> None)

[<Fact>]
let ``F2 asks for the user menu`` () =
    let fs = tree ()

    let result = update fs UserMenuRequested (loaded fs)

    Assert.Equal<Effect list>([ ShowUserMenu ], result.Effects)

[<Fact>]
let ``a chosen entry is expanded and run from the directory of the active panel`` () =
    let fs = tree ()

    let result = update fs (MenuCommandChosen(entry "show %p", listFile)) (loaded fs)

    Assert.Equal<Effect list>([ RunCommand showing ], result.Effects)

[<Fact>]
let ``a run keeps the way of running and the elevation the entry names`` () =
    let fs = tree ()
    let raised = { entry "show %p" with Run = InShellKeptOpen; Elevation = AsAdministrator }

    let result = update fs (MenuCommandChosen(raised, listFile)) (loaded fs)

    let expected = { showing with Run = InShellKeptOpen; Elevation = AsAdministrator }
    Assert.Equal<Effect list>([ RunCommand expected ], result.Effects)

[<Fact>]
let ``a run from a panel inside an archive starts in the directory that holds the archive`` () =
    let fs = ArchiveFixture.packed ()
    let state = ArchiveFixture.standingInside fs Left

    let result = update fs (MenuCommandChosen(entry "show", listFile)) state

    Assert.Equal<FileSystemPath list>([ fp (at "start") ], startedIn result.Effects)

[<Fact>]
let ``a line the panels refuse is reported and nothing runs`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left 0

    let result = update fs (MenuCommandChosen(entry "show %p", listFile)) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ ReportExpansionRefusal(NoEntryChosen Left) ], result.Effects)

[<Fact>]
let ``a command that started is remembered on the history`` () =
    let fs = tree ()

    let result = update fs (CommandFinished(showing, Ok())) (loaded fs)

    Assert.Equal<Effect list>([ ChangeHistory(Remember(Ran showing)) ], result.Effects)

[<Fact>]
let ``a command whose elevation was turned down is silence`` () =
    let fs = tree ()
    let state = loaded fs

    let result = update fs (CommandFinished(showing, Error(ElevationDeclined(fp "show")))) state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a command that could not start is reported and not remembered`` () =
    let fs = tree ()
    let failure = ProgramNotFound(Expect.some (ProgramPath.tryCreate "show"))

    let result = update fs (CommandFinished(showing, Error failure)) (loaded fs)

    Assert.Equal<Effect list>([ ReportCommandError failure ], result.Effects)

[<Fact>]
let ``a command chosen on the history runs again exactly as it ran`` () =
    let fs = tree ()
    let answer = HistoryAnswered { Tab = RunHistoryTab; Choice = Some(RunAgain(StartedCommand showing)) }

    let result = update fs answer (loaded fs |> setCursor Left 0)

    Assert.Equal<Effect list>([ RunCommand showing ], result.Effects)
