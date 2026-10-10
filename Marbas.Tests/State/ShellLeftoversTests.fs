module Marbas.Tests.State.ShellLeftoversTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Messages
open Marbas.State
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private earlier =
    { ProcessId = 3980
      Started = DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.Zero) }

let private leftByEarlier (fs: MockFileSystem) =
    fs.AddFile(at "temp/Marbas/earlier/1/left.txt", MockFileData "left")
    Workspace.claim fs earlier (fp (at "temp/Marbas/earlier")) |> Expect.ok

let private whereNobodyRuns (fs: MockFileSystem) =
    { Fakes.dependencies fs (at "start") with
        Processes.IsRunning = fun _ -> false }

[<Fact>]
let ``the sweep claims the working directory of the session for its process`` () =
    let fs = MockFileSystem()
    let deps = Fakes.dependencies fs (at "start")

    Shell.interpret deps ignore SweepLeftovers

    Assert.Equal<string>(SessionOwner.lines deps.Processes.Owner, fs.File.ReadAllLines(at "temp/Marbas/session/owner"))

[<Fact>]
let ``the sweep removes what a session that no longer runs left behind`` () =
    let fs = MockFileSystem()
    leftByEarlier fs

    Shell.interpret (whereNobodyRuns fs) ignore SweepLeftovers

    Assert.False(fs.Directory.Exists(at "temp/Marbas/earlier"))

[<Fact>]
let ``the sweep keeps what a running session works in`` () =
    let fs = MockFileSystem()
    leftByEarlier fs
    let deps = Fakes.dependencies fs (at "start")

    Shell.interpret deps ignore SweepLeftovers

    Assert.True(fs.File.Exists(at "temp/Marbas/earlier/1/left.txt"))

[<Fact>]
let ``the sweep asks about the owner the directory names`` () =
    let fs = MockFileSystem()
    leftByEarlier fs
    let asked = ResizeArray<SessionOwner>()

    let deps =
        { Fakes.dependencies fs (at "start") with
            Processes.IsRunning =
                fun owner ->
                    asked.Add owner
                    true }

    Shell.interpret deps ignore SweepLeftovers

    Assert.Equal<SessionOwner list>([ earlier ], List.ofSeq asked)

[<Fact>]
let ``a leftover that refuses deletion leaves a log line and no dialog`` () =
    let held = at "temp/Marbas/earlier/1/left.txt"
    let fs = Fakes.HeldOpen held
    leftByEarlier fs
    let logged = ResizeArray<LogEntry>()
    let reported = ResizeArray<Ports.ErrorReport>()

    let deps =
        { whereNobodyRuns fs with
            Log = logged.Add
            Prompts.ReportError = reported.Add }

    Shell.interpret deps ignore SweepLeftovers

    Assert.Contains(
        LeftoverKept(
            fp (at "temp/Marbas/earlier"),
            OperationErrorMessage.describe (Operations.OperationFailed(fp held, Fakes.HeldOpenReason))
        ),
        logged
    )

    Assert.Empty reported

[<Fact>]
let ``a working directory that cannot be claimed leaves a log line and no dialog`` () =
    let fs = Fakes.UnwritableDirectory(at "temp/Marbas/session")
    let logged = ResizeArray<LogEntry>()
    let reported = ResizeArray<Ports.ErrorReport>()

    let deps =
        { Fakes.dependencies fs (at "start") with
            Log = logged.Add
            Prompts.ReportError = reported.Add }

    Shell.interpret deps ignore SweepLeftovers

    Assert.Contains(
        WorkspaceUnclaimed(
            fp (at "temp/Marbas/session"),
            OperationErrorMessage.describe (Operations.AccessDenied(fp (at "temp/Marbas/session")))
        ),
        logged
    )

    Assert.Empty reported

[<Fact>]
let ``the sweep is handed to the runner that works off the UI thread`` () =
    let fs = MockFileSystem()
    leftByEarlier fs
    let handed = ResizeArray<unit -> unit>()

    let deps =
        { whereNobodyRuns fs with
            Runners.Background = handed.Add }

    Shell.interpret deps ignore SweepLeftovers

    Assert.Single handed |> ignore
    Assert.True(fs.Directory.Exists(at "temp/Marbas/earlier"))
    Assert.False(fs.Directory.Exists(at "temp/Marbas/session"))
