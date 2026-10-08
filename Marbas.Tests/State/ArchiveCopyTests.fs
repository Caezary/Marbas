module Marbas.Tests.State.ArchiveCopyTests

open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private inside raw =
    ArchivePath.tryCreate raw |> Expect.some

let private start = fp (at "start")

[<Fact>]
let ``copying the entry under the cursor out of an archive extracts it into the other panel`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 2

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(
        [ CopyPlaces([ FromArchive(archive, [ inside "notes.txt" ]) ], ToDirectory start) ],
        result.Effects
    )

[<Fact>]
let ``the marked entries of an archive panel are the targets`` () =
    let fs = packed ()

    let state =
        standingInside fs Left
        |> setCursor Left 1
        |> toggleSelection Left
        |> setCursor Left 2
        |> toggleSelection Left

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(
        [ CopyPlaces([ FromArchive(archive, [ inside "docs"; inside "notes.txt" ]) ], ToDirectory start) ],
        result.Effects
    )

[<Fact>]
let ``a level below the top names its targets by where they stand`` () =
    let fs = packed ()

    let state =
        standingInside fs Left
        |> setCursor Left 1
        |> enter fs
        |> Expect.ok
        |> setCursor Left 2

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(
        [ CopyPlaces([ FromArchive(archive, [ inside "docs/inner.txt" ]) ], ToDirectory start) ],
        result.Effects
    )

[<Fact>]
let ``the way out of an archive is nothing to copy`` () =
    let fs = packed ()
    let state = standingInside fs Left

    let result = update fs CopyRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``a panel on disk still copies the paths it points at`` () =
    let fs = packed ()
    let state = loaded fs |> setCursor Left 1

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(
        [ CopyPlaces([ FromDisk [ fp (at "start/backup.zip") ] ], ToDirectory start) ],
        result.Effects
    )

[<Fact>]
let ``a mark whose entry made it out is dropped and one that failed stays`` () =
    let fs = packed ()

    let state =
        standingInside fs Left
        |> setCursor Left 1
        |> toggleSelection Left
        |> setCursor Left 2
        |> toggleSelection Left

    let results: Operations.ItemResult list =
        [ { Source = Operations.ItemInArchive(archive, inside "docs")
            Outcome = Ok() }
          { Source = Operations.ItemInArchive(archive, inside "notes.txt")
            Outcome = Error(Operations.Cancelled(fp (at "start/backup.zip/notes.txt"))) } ]

    let result = update fs (CopyFinished results) state

    Assert.Equal(InArchive(archive, ArchivePath.root), result.State.Left.Path)
    Assert.Equal<Set<EntryName>>(Set.singleton (EntryName.create "notes.txt"), result.State.Left.Selected)
