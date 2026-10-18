module Marbas.Tests.State.LaunchHistoryTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.FileSystem.DirectoryListing
open Marbas.Messages
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private tree () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/tool.exe", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private Docs = 1

let private Notes = 2

let private Tool = 3

let private loaded (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok

let private ran effect =
    match effect with
    | RunProgram request -> Some request
    | _ -> None

let private started (result: UpdateResult) =
    result.Effects |> List.choose ran |> List.exactlyOne

let private historyChanges effects =
    effects
    |> List.choose (fun effect ->
        match effect with
        | ChangeHistory change -> Some change
        | _ -> None)

let private finishedWell (fs: IFileSystem) state request =
    update fs (LaunchFinished(request, Ok())) state

let private answered choice =
    HistoryAnswered
        { Tab = RunHistoryTab
          Choice = Some choice }

let private launchOf target =
    { Target = fp (at target)
      Program = ProgramPath.tryCreate "installer.exe"
      WorkingDirectory = fp (at "start")
      Elevation = AsAdministrator }

[<Fact>]
let ``a file opened with Enter is remembered once it has started`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Notes
    let request = update fs EntryOpened state |> started

    let result = finishedWell fs state request

    Assert.Equal<HistoryChange list>([ Remember(Launched request) ], historyChanges result.Effects)

[<Fact>]
let ``a file opened with a program chosen for it is remembered with that program`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Notes

    let choice =
        { Program = Expect.some (ProgramPath.tryCreate "code.exe")
          Use = ThisTimeOnly }

    let request =
        update fs (HandlerChosen(fp (at "start/notes.txt"), choice)) state |> started

    let result = finishedWell fs state request

    Assert.Equal<HistoryChange list>([ Remember(Launched request) ], historyChanges result.Effects)
    Assert.Equal(ProgramPath.tryCreate "code.exe", request.Program)

[<Fact>]
let ``a program run as administrator is remembered as run that way`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Tool
    let request = update fs ElevatedRunRequested state |> started

    let result = finishedWell fs state request

    Assert.Equal<HistoryChange list>([ Remember(Launched request) ], historyChanges result.Effects)
    Assert.Equal(AsAdministrator, request.Elevation)

[<Fact>]
let ``a directory shown in the system file manager is not remembered`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Docs
    let request = update fs OpenWithRequested state |> started

    let result = finishedWell fs state request

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``a drive handed to the system file manager is not remembered`` () =
    let fs = tree ()
    let state = loaded fs

    let request =
        update fs (DriveHandedOver(MountedDrive(Fakes.driveAt (at "")))) state
        |> started

    let result = finishedWell fs state request

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``a start that failed is not remembered`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Notes
    let request = update fs EntryOpened state |> started

    let result =
        update fs (LaunchFinished(request, Error(NothingAssociated request.Target))) state

    Assert.Empty(historyChanges result.Effects)

[<Fact>]
let ``a file started again from the history starts as it started the first time`` () =
    let fs = tree ()
    let state = loaded fs

    let result =
        update fs (answered (RunAgain(StartedFile(launchOf "start/tool.exe")))) state

    Assert.Equal<Effect list>([ RunProgram(launchOf "start/tool.exe") ], result.Effects)

[<Fact>]
let ``going to a started file stands the active panel beside it with the cursor on it`` () =
    let fs = tree ()
    let state = loaded fs |> activate Right

    let result =
        update fs (answered (GoTo(Place.OnDisk(fp (at "start/notes.txt"))))) state

    Assert.Equal(OnDisk(fp (at "start")), result.State.Right.Path)
    Assert.Equal(Notes, result.State.Right.Cursor)

[<Fact>]
let ``going to a file whose directory is gone is reported and the panel stays`` () =
    let fs = tree ()
    let state = loaded fs

    let result =
        update fs (answered (GoTo(Place.OnDisk(fp (at "gone/notes.txt"))))) state

    Assert.Equal(OnDisk(fp (at "start")), result.State.Left.Path)
    Assert.Equal<Effect list>([ Report(Problem.Listing(DirectoryNotFound(fp (at "gone")))) ], result.Effects)
