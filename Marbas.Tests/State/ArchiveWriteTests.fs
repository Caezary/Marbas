module Marbas.Tests.State.ArchiveWriteTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private inside raw = ArchivePath.tryCreate raw |> Expect.some

let private loose = fp (at "start/loose.txt")

let private withLoose () =
    let fs = packed ()
    fs.AddFile(at "start/loose.txt", MockFileData "loose")
    fs

let private tarball = fp (at "start/backup.tar")

let private withTarball () =
    let fs = tarredAs (at "start/backup.tar")
    fs.AddFile(at "start/loose.txt", MockFileData "loose")
    fs

let private names panel = panel.Entries |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq

let private atLoose state = setCursor Left 2 state

[<Fact>]
let ``a copy into a panel inside an archive adds the targets at that level`` () =
    let fs = withLoose ()
    let state = standingInside fs Right |> atLoose

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(
        [ CopyPlaces([ FromDisk [ loose ] ], ToArchive(archive, ArchivePath.root)) ],
        result.Effects
    )

[<Fact>]
let ``a copy into a level below the top of an archive lands at that level`` () =
    let fs = withLoose ()

    let state =
        standingInside fs Right
        |> activate Right
        |> setCursor Right 1
        |> enter fs
        |> Expect.ok
        |> activate Left
        |> atLoose

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>([ CopyPlaces([ FromDisk [ loose ] ], ToArchive(archive, inside "docs")) ], result.Effects)

[<Fact>]
let ``a move into a panel inside an archive asks for no target and moves straight in`` () =
    let fs = withLoose ()
    let state = standingInside fs Right |> atLoose

    let result = update fs MoveRequested state

    Assert.Equal<Effect list>(
        [ MovePlaces([ FromDisk [ loose ] ], ToArchive(archive, ArchivePath.root)) ],
        result.Effects
    )

[<Fact>]
let ``a move out of a panel inside an archive takes the entries to the other panel`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 2

    let result = update fs MoveRequested state

    Assert.Equal<Effect list>(
        [ MovePlaces([ FromArchive(archive, [ inside "notes.txt" ]) ], ToDirectory(fp (at "start"))) ],
        result.Effects
    )

[<Fact>]
let ``a delete inside an archive is asked for good and removes the entries`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 2

    let asked = update fs (DeleteRequested ToTrash) state
    let confirmed = update fs (DeleteConfirmed Permanently) state

    Assert.Equal<Effect list>(
        [ InspectDelete({ Probed = []; ReachesContents = false }, Permanently) ],
        asked.Effects
    )

    Assert.Equal<Effect list>(
        [ DeletePlaces([ FromArchive(archive, [ inside "notes.txt" ]) ], Permanently) ],
        confirmed.Effects
    )

[<Fact>]
let ``a wipe inside an archive is asked as a delete for good`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 2

    let asked = update fs (DeleteRequested ByWiping) state

    Assert.Equal<Effect list>(
        [ InspectDelete({ Probed = []; ReachesContents = false }, Permanently) ],
        asked.Effects
    )

[<Fact>]
let ``a directory among the targets inside an archive needs nothing read to warn about its contents`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 1

    let result = update fs (DeleteRequested ToTrash) state

    Assert.Equal<Effect list>(
        [ InspectDelete({ Probed = []; ReachesContents = true }, Permanently) ],
        result.Effects
    )

[<Fact>]
let ``the scope that came back names the entries inside the archive`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 1

    let result = update fs (DeleteScopeLoaded(WithDirectoryContents, Permanently)) state

    Assert.Equal<Effect list>(
        [ ConfirmDelete
              { Targets = [ en "docs" ]
                Scope = WithDirectoryContents
                Mode = Permanently } ],
        result.Effects
    )

[<Fact>]
let ``a directory made inside an archive is in it and under the cursor`` () =
    let fs = packed ()
    let state = standingInside fs Left

    let asked = update fs MakeDirectoryRequested state
    let made = update fs (NameEntered(en "fresh")) state

    Assert.Equal<Effect list>([ PromptForName None ], asked.Effects)
    Assert.Equal<string list>([ ".."; "docs"; "fresh"; "notes.txt" ], names made.State.Left)
    Assert.Equal(Some(en "fresh"), PanelState.cursorEntry made.State.Left |> Option.map _.Name)
    Assert.Equal(Some(en "fresh"), made.State.LastCreatedDirectory)

    Assert.Contains(
        "fresh",
        ArchiveListing.read fs archive ArchivePath.root
        |> Expect.ok
        |> Seq.map (_.Name >> EntryName.value) |> List.ofSeq
    )

[<Fact>]
let ``a name already in the archive is refused`` () =
    let fs = packed ()
    let state = standingInside fs Left

    let result = update fs (NameEntered(en "docs")) state

    Assert.Equal<Effect list>(
        [ ReportOperationError(Operations.TargetExists(fp (at "start/backup.zip/docs"))) ],
        result.Effects
    )

let private other = fp (at "start/other.zip")

let private beside (fs: MockFileSystem) =
    Archive.write fs Operations.unattended (request Zip (at "start/other.zip")) [ fp (at "work/notes.txt") ]
    |> ignore

    fs

let private standingInBoth (fs: MockFileSystem) =
    loaded fs
    |> activate Right
    |> setCursor Right 2
    |> enter fs
    |> Expect.ok
    |> activate Left
    |> setCursor Left 1
    |> enter fs
    |> Expect.ok
    |> activate Left
    |> setCursor Left 2

[<Fact>]
let ``a copy between two archives carries the entry across`` () =
    let fs = packed () |> beside

    let result = update fs CopyRequested (standingInBoth fs)

    Assert.Equal<Effect list>(
        [ CopyPlaces([ FromArchive(archive, [ inside "notes.txt" ]) ], ToArchive(other, ArchivePath.root)) ],
        result.Effects
    )

[<Fact>]
let ``a move between two archives carries the entry across`` () =
    let fs = packed () |> beside

    let result = update fs MoveRequested (standingInBoth fs)

    Assert.Equal<Effect list>(
        [ MovePlaces([ FromArchive(archive, [ inside "notes.txt" ]) ], ToArchive(other, ArchivePath.root)) ],
        result.Effects
    )

[<Fact>]
let ``a move between archives needs the one it takes from to be changeable`` () =
    let fs = beside (tarredAs (at "start/backup.tar"))
    let unchangeable = fp (at "start/backup.tar")
    let state = standingInBoth fs

    let moved = update fs MoveRequested state
    let copied = update fs CopyRequested state

    Assert.Equal<Effect list>(
        [ ReportOperationError(Operations.ArchiveFailed(KindCannotBeChanged unchangeable)) ],
        moved.Effects
    )

    Assert.Equal<Effect list>(
        [ CopyPlaces([ FromArchive(unchangeable, [ inside "notes.txt" ]) ], ToArchive(other, ArchivePath.root)) ],
        copied.Effects
    )

let private refusal = ReportOperationError(Operations.ArchiveFailed(KindCannotBeChanged tarball))

[<Fact>]
let ``a copy into a kind that cannot be changed is refused by name`` () =
    let fs = withTarball ()
    let state = standingInside fs Right |> atLoose

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>([ refusal ], result.Effects)

[<Fact>]
let ``a move out of a kind that cannot be changed is refused where a copy works`` () =
    let fs = withTarball ()
    let state = standingInside fs Left |> setCursor Left 2

    let moved = update fs MoveRequested state
    let copied = update fs CopyRequested state

    Assert.Equal<Effect list>([ refusal ], moved.Effects)

    Assert.Equal<Effect list>(
        [ CopyPlaces([ FromArchive(tarball, [ inside "notes.txt" ]) ], ToDirectory(fp (at "start"))) ],
        copied.Effects
    )

[<Fact>]
let ``a directory is not offered inside a kind that cannot be changed`` () =
    let fs = withTarball ()
    let state = standingInside fs Left

    let result = update fs MakeDirectoryRequested state

    Assert.Equal<Effect list>([ refusal ], result.Effects)

[<Fact>]
let ``a delete inside a kind that cannot be changed is refused before it is confirmed`` () =
    let fs = withTarball ()
    let state = standingInside fs Left |> setCursor Left 2

    let result = update fs (DeleteRequested ToTrash) state

    Assert.Equal<Effect list>([ refusal ], result.Effects)
