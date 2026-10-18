module Marbas.Tests.State.HistoryPopupTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Locations
open Marbas.FileSystem.DirectoryListing
open Marbas.Messages
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddDirectory(at "backup")
    fs

let private loadedBoth (fs: IFileSystem) =
    create (fp (at "start"))
    |> loadPanel fs Left (fp (at "start"))
    |> Expect.ok
    |> loadPanel fs Right (fp (at "backup"))
    |> Expect.ok

let private answered tab choice =
    HistoryAnswered { Tab = tab; Choice = choice }

[<Fact>]
let ``the history opens on the directories the first time`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs HistoryRequested state

    Assert.Equal<Effect list>([ ShowHistory FolderHistoryTab ], result.Effects)

[<Fact>]
let ``the history opens again on the tab it was left on`` () =
    let fs = mockFs ()
    let state = (update fs (answered ViewHistoryTab None) (loadedBoth fs)).State

    let result = update fs HistoryRequested state

    Assert.Equal<Effect list>([ ShowHistory ViewHistoryTab ], result.Effects)

[<Fact>]
let ``a history closed without a choice leaves the panels where they stand`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs (answered RunHistoryTab None) state

    Assert.Equal(OnDisk(fp (at "start")), result.State.Left.Path)
    Assert.Empty result.Effects

[<Fact>]
let ``a directory chosen from the history is where the active panel stands`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> activate Right

    let result =
        update fs (answered FolderHistoryTab (Some(OpenPlace(Place.OnDisk(fp (at "start/docs")))))) state

    Assert.Equal(OnDisk(fp (at "start/docs")), result.State.Right.Path)
    Assert.Equal(OnDisk(fp (at "start")), result.State.Left.Path)

[<Fact>]
let ``a directory chosen from the history moves to the top of it`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result =
        update fs (answered FolderHistoryTab (Some(OpenPlace(Place.OnDisk(fp (at "start/docs")))))) state

    Assert.Contains(ChangeHistory(Remember(Visited(Place.OnDisk(fp (at "start/docs"))))), result.Effects)

[<Fact>]
let ``a level inside an archive chosen from the history is walked back into`` () =
    let fs = ArchiveFixture.packed ()
    let state = ArchiveFixture.loaded fs |> activate Left
    let docs = Expect.some (ArchivePath.tryCreate "docs")

    let result =
        update fs (answered FolderHistoryTab (Some(OpenPlace(Place.InArchive(ArchiveFixture.archive, docs))))) state

    Assert.Equal(InArchive(ArchiveFixture.archive, docs), result.State.Left.Path)

[<Fact>]
let ``a directory from the history that is gone is reported and the panel stays`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result =
        update fs (answered FolderHistoryTab (Some(OpenPlace(Place.OnDisk(fp (at "gone")))))) state

    Assert.Equal(OnDisk(fp (at "start")), result.State.Left.Path)
    Assert.Equal<Effect list>([ Report(Problem.Listing(DirectoryNotFound(fp (at "gone")))) ], result.Effects)
