module Marbas.Tests.State.WatchTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs/inner")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddDirectory(at "backup")
    fs

let private watches effects =
    effects
    |> List.filter (fun effect ->
        match effect with
        | WatchLocations _ -> true
        | _ -> false)

let private loadedBoth (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok

[<Fact>]
let ``walking into a directory answers with the paths on screen`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 1

    let result = update fs WalkIntoRequested state

    Assert.Equal<Effect list>([ WatchLocations [ fp (at "start/docs"); fp (at "backup") ] ], watches result.Effects)

[<Fact>]
let ``walking out answers with the directory walked out to`` () =
    let fs = mockFs ()

    let state =
        loadedBoth fs |> loadPanel fs Left (fp (at "start/docs")) |> Expect.ok |> setCursor Left 0

    let result = update fs WalkIntoRequested state

    Assert.Equal<Effect list>([ WatchLocations [ fp (at "start"); fp (at "backup") ] ], watches result.Effects)

[<Fact>]
let ``a panel standing inside an archive is watched by the archive file`` () =
    let fs = ArchiveFixture.packed ()
    let state = ArchiveFixture.loaded fs |> activate Left |> setCursor Left 1

    let result = update fs WalkIntoRequested state

    Assert.Equal<Effect list>(
        [ WatchLocations [ fp (at "start/backup.zip"); fp (at "start") ] ],
        watches result.Effects
    )

[<Fact>]
let ``both panels in one directory are watched once`` () =
    let fs = mockFs ()

    let state =
        loadedBoth fs |> loadPanel fs Right (fp (at "start/docs")) |> Expect.ok |> setCursor Left 1

    let result = update fs WalkIntoRequested state

    Assert.Equal<Effect list>([ WatchLocations [ fp (at "start/docs") ] ], watches result.Effects)

[<Fact>]
let ``a panel sent to a root is watched where it lands`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs (PathOpened(Right, fp (at "start/docs"))) state

    Assert.Equal<Effect list>([ WatchLocations [ fp (at "start"); fp (at "start/docs") ] ], watches result.Effects)

[<Fact>]
let ``a panel sent to the root of its volume is watched where it lands`` () =
    let fs = mockFs ()
    let state = loadedBoth fs
                |> loadPanel fs Left (fp (at "start/docs"))
                |> Expect.ok

    let result = update fs VolumeRootRequested state

    Assert.Equal<Effect list>([ WatchLocations [ fp (at ""); fp (at "backup") ] ], watches result.Effects)

[<Fact>]
let ``a re-read of the same directories asks for no watch`` () =
    let fs = mockFs ()
    let state = loadedBoth fs
    fs.AddFile(at "start/added.txt", MockFileData "new")

    let result = update fs RefreshRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``a cursor moved within a panel asks for no watch`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs (CursorMoved { Side = Left; Index = 2 }) state

    Assert.Empty result.Effects

[<Fact>]
let ``a tab opened on the directories already on screen asks for no watch`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs TabOpened state

    Assert.Equal<Effect list>([ SaveSettings(settings fs result.State) ], result.Effects)

[<Fact>]
let ``switching to a tab standing elsewhere answers with its paths`` () =
    let fs = mockFs ()
    let opened = (update fs TabOpened (loadedBoth fs)).State
    let walked = (update fs WalkIntoRequested (setCursor Left 1 opened)).State

    let result = update fs (TabSwitched 0) walked

    Assert.Contains(WatchLocations [ fp (at "start"); fp (at "backup") ], result.Effects)

[<Fact>]
let ``closing a tab answers with the paths of the tab it lands on`` () =
    let fs = mockFs ()
    let opened = (update fs TabOpened (loadedBoth fs)).State
    let walked = (update fs WalkIntoRequested (setCursor Left 1 opened)).State

    let result = update fs TabClosed walked

    Assert.Contains(WatchLocations [ fp (at "start"); fp (at "backup") ], result.Effects)
