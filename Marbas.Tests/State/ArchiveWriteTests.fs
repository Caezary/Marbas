module Marbas.Tests.State.ArchiveWriteTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
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

let private names panel = panel.Entries |> List.map (_.Name >> EntryName.value)

let private atLoose state = setCursor Left 2 state

[<Fact>]
let ``a copy into a panel inside an archive adds the targets at that level`` () =
    let fs = withLoose ()
    let state = standingInside fs Right |> atLoose

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>([ AddEntries(archive, ArchivePath.root, [ loose ]) ], result.Effects)

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

    Assert.Equal<Effect list>([ AddEntries(archive, inside "docs", [ loose ]) ], result.Effects)

[<Fact>]
let ``a move into a panel inside an archive asks for no target and moves straight in`` () =
    let fs = withLoose ()
    let state = standingInside fs Right |> atLoose

    let result = update fs MoveRequested state

    Assert.Equal<Effect list>([ MoveEntriesIn(archive, ArchivePath.root, [ loose ]) ], result.Effects)

[<Fact>]
let ``a move out of a panel inside an archive takes the entries to the other panel`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 2

    let result = update fs MoveRequested state

    Assert.Equal<Effect list>(
        [ MoveEntriesOut(archive, [ inside "notes.txt" ], fp (at "start")) ],
        result.Effects
    )

[<Fact>]
let ``a delete inside an archive is confirmed for good and removes the entries`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 2

    let asked = update fs (DeleteRequested ToTrash) state
    let confirmed = update fs (DeleteConfirmed Permanently) state

    Assert.Equal<Effect list>(
        [ ConfirmDelete
              { Targets = [ en "notes.txt" ]
                Scope = NamedEntriesOnly
                Mode = Permanently } ],
        asked.Effects
    )

    Assert.Equal<Effect list>([ RemoveEntries(archive, [ inside "notes.txt" ]) ], confirmed.Effects)

[<Fact>]
let ``a directory among the targets inside an archive warns about its contents`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 1

    let result = update fs (DeleteRequested ToTrash) state

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
    Assert.Equal(Some(en "fresh"), cursorEntry made.State.Left |> Option.map _.Name)
    Assert.Equal(Some(en "fresh"), made.State.LastCreatedDirectory)

    Assert.Contains(
        "fresh",
        ArchiveListing.read fs archive ArchivePath.root
        |> Expect.ok
        |> List.map (_.Name >> EntryName.value)
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

[<Fact>]
let ``a copy between two archives asks for nothing`` () =
    let fs = packed ()

    let state =
        standingInside fs Right
        |> activate Left
        |> setCursor Left 1
        |> enter fs
        |> Expect.ok
        |> setCursor Left 2

    let result = update fs CopyRequested state

    Assert.Empty result.Effects

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
        [ ExtractEntries(tarball, [ inside "notes.txt" ], fp (at "start")) ],
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
