module Marbas.Tests.State.Shell.ShellWatchTests

open Xunit
open Marbas.FileSystem
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``the paths under watch are handed to the watch`` () =
    let _, dispatch = capture ()
    let mutable watched = []

    let deps =
        { noDeps () with
            Watches.Locations = fun watch -> watched <- watch.Paths }

    Shell.interpret deps dispatch (WatchLocations [ fp (at "start"); fp (at "backup") ])

    Assert.Equal<FileSystemPath list>([ fp (at "start"); fp (at "backup") ], watched)

[<Fact>]
let ``what the watch reports is dispatched as an external change`` () =
    let sent, dispatch = capture ()
    let mutable report = ignore

    let deps =
        { noDeps () with
            Watches.Locations = fun watch -> report <- watch.Notify }

    Shell.interpret deps dispatch (WatchLocations [ fp (at "start") ])
    report ()

    Assert.Equal<Message list>([ ExternalChangeDetected ], List.ofSeq sent)
