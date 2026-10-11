module Marbas.Tests.State.Update.UpdateTransferTests

open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Update.UpdateFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

[<Fact>]
let ``copy requested aims at the other panel's directory`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs CopyRequested state

    Assert.Equal<Effect list>(
        [ CopyPlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToDirectory(fp (at "backup"))) ],
        result.Effects
    )

[<Fact>]
let ``copy requested on the parent entry raises no effect`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs CopyRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished copy shows the new entry in the other panel`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    let results = copyAll fs unattended [ fp (at "start/notes.txt") ] (fp (at "backup"))

    let result = update fs (CopyFinished results) state

    Assert.Contains("notes.txt", names result.State.Right.Entries)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished copy reports the items that failed`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let failed =
        [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
            Outcome = Error(AccessDenied(fp (at "backup/notes.txt"))) } ]

    let result = update fs (CopyFinished failed) state

    Assert.Equal<Effect list>([ Report(Problem.ItemFailures failed) ], result.Effects)

[<Fact>]
let ``a finished copy reports the panel it could no longer read`` () =
    let fs = mockFs ()
    let state = loadedBoth fs
    let results = copyAll fs unattended [ fp (at "start/notes.txt") ] (fp (at "backup"))
    fs.Directory.Delete(at "backup", true)

    let result = update fs (CopyFinished results) state

    Assert.Equal<Effect list>([ Report(Problem.Operation(SourceNotFound(fp (at "backup")))) ], result.Effects)
    Assert.Equal(state, result.State)

[<Fact>]
let ``move requested offers the other panel's path`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs MoveRequested state

    Assert.Equal<Effect list>([ PromptForTarget(fp (at "backup")) ], result.Effects)

[<Fact>]
let ``move requested on the parent entry raises no effect`` () =
    let fs = mockFs ()
    let state = loadedBoth fs

    let result = update fs MoveRequested state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``an entered path moves the targets into it`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs (TargetEntered(at "backup")) state

    Assert.Equal<Effect list>(
        [ MovePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToDirectory(fp (at "backup"))) ],
        result.Effects
    )

[<Fact>]
let ``a bare name renames the only target`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs (TargetEntered "readme.txt") state

    Assert.Equal<Effect list>([ RenameTarget(fp (at "start/notes.txt"), en "readme.txt") ], result.Effects)

[<Fact>]
let ``a bare name with several targets names a directory beside them`` () =
    let fs = mockFs ()

    let state =
        loadedBoth fs
        |> setCursor Left 2
        |> toggleSelectionMoving Downward Left
        |> toggleSelectionMoving Downward Left

    let result = update fs (TargetEntered "archive") state

    Assert.Equal<Effect list>(
        [ MovePlaces(
              [ FromDisk [ fp (at "start/notes.txt"); fp (at "start/setup.fs") ] ],
              ToDirectory(fp (at "start/archive"))
          ) ],
        result.Effects
    )

[<Fact>]
let ``a blank target changes nothing`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2

    let result = update fs (TargetEntered "   ") state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished move shows the entry in the other panel`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    let results = moveAll fs unattended [ fp (at "start/notes.txt") ] (fp (at "backup"))

    let result = update fs (MoveFinished results) state

    Assert.Contains("notes.txt", names result.State.Right.Entries)
    Assert.DoesNotContain("notes.txt", names result.State.Left.Entries)
    Assert.Empty result.Effects

[<Fact>]
let ``a finished rename puts the cursor on the new name`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    startRename fs (fp (at "start/notes.txt")) (en "zzz.txt") |> ignore

    let result = update fs (RenameFinished(en "zzz.txt", Ok())) state

    Assert.Equal(
        Some "zzz.txt",
        PanelState.cursorEntry result.State.Left
        |> Option.map (_.Name >> EntryName.value)
    )

    Assert.Empty result.Effects

[<Fact>]
let ``a failed rename is reported and leaves the panel alone`` () =
    let fs = mockFs ()
    let state = loadedBoth fs |> setCursor Left 2
    let taken = TargetExists(fp (at "start/docs"))

    let result = update fs (RenameFinished(en "docs", Error taken)) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ Report(Problem.Operation taken) ], result.Effects)
