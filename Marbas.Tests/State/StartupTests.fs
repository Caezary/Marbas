module Marbas.Tests.State.StartupTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddDirectory(at "backup")
    fs

[<Fact>]
let ``with nothing saved both panels are to start on the start path`` () =
    let deps = Fakes.dependencies (mockFs ()) (at "start")

    let state = Startup.restored deps

    Assert.Equal(OnDisk(fp (at "start")), (panel Left state).Path)
    Assert.Equal(OnDisk(fp (at "start")), (panel Right state).Path)

[<Fact>]
let ``what an earlier session saved is where the panels are to start`` () =
    let fs = mockFs ()
    let deps = Fakes.dependencies fs (at "start")
    let saved = Settings.forPath (fp (at "backup"))
    Settings.save fs deps.SettingsPath saved |> Expect.ok

    let state = Startup.restored deps

    Assert.Equal(OnDisk(fp (at "backup")), (panel Left state).Path)
    Assert.Equal(OnDisk(fp (at "backup")), (panel Right state).Path)

[<Fact>]
let ``the first steps open the directory each panel is to start in`` () =
    let deps = Fakes.dependencies (mockFs ()) (at "start")
    let state = Startup.restored deps
    let sent = ResizeArray<Message>()

    Startup.firstSteps deps sent.Add (fun () -> state)

    Assert.Equal<Message list>(
        [ PathOpened(Left, fp (at "start")); PathOpened(Right, fp (at "start")) ],
        List.ofSeq sent
    )

[<Fact>]
let ``the first steps put under watch where the panels stand by then`` () =
    let fs = mockFs ()
    let watched = ResizeArray<FileSystemPath list>()

    let deps =
        { Fakes.dependencies fs (at "start") with
            Watches.Locations = fun watch -> watched.Add watch.Paths }

    let opened =
        Startup.restored deps |> loadPanel fs Right (fp (at "backup")) |> Expect.ok

    Startup.firstSteps deps ignore (fun () -> opened)

    Assert.Equal<FileSystemPath list list>([ [ fp (at "start"); fp (at "backup") ] ], List.ofSeq watched)

[<Fact>]
let ``the first steps hand the sweep of what earlier sessions left to the background`` () =
    let handed = ResizeArray<unit -> unit>()

    let deps =
        { Fakes.dependencies (mockFs ()) (at "start") with
            Runners.Background = fun request -> handed.Add request.Work }

    let state = Startup.restored deps

    Startup.firstSteps deps ignore (fun () -> state)

    Assert.Single handed |> ignore
