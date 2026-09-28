module Marbas.Tests.State.RuntimeQueueTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Runtime
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs/inner")
    fs.AddDirectory(at "backup")
    fs :> IFileSystem

let private twoTabs (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok
    |> openTab fs
    |> loadPanel fs Left (fp (at "start/docs"))
    |> Expect.ok

[<Fact>]
let ``a dispatch from inside an effect waits for the outer effects to finish`` () =
    let fs = mockFs ()
    let seen = ResizeArray<Effect>()

    let interpret dispatch effect =
        seen.Add effect

        match effect with
        | SaveSettings _ -> dispatch (PathOpened(Right, fp (at "start/docs/inner")))
        | _ -> ()

    let runtime = Runtime(twoTabs fs, step fs, interpret, ignore)

    runtime.Dispatch(TabCycled Backward)

    Assert.Equal(4, seen.Count)
    Assert.Equal(WatchLocations [ fp (at "start"); fp (at "backup") ], seen[1])
    Assert.Equal(WatchLocations [ fp (at "start"); fp (at "start/docs/inner") ], seen[2])
    Assert.Equal(ChangeHistory(Remember(Visited(Place.OnDisk(fp (at "start/docs/inner"))))), seen[3])
