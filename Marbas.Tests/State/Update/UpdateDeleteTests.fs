module Marbas.Tests.State.Update.UpdateDeleteTests

open System.IO.Abstractions.TestingHelpers
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
let ``delete requested asks what the cursor entry reaches into`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs (DeleteRequested ToTrash) state

    Assert.Equal<Effect list>(
        [ InspectDelete(
              { Probed = [ fp (at "start/notes.txt") ]
                AtLeast = NamedEntriesOnly },
              ToTrash
          ) ],
        result.Effects
    )

[<Fact>]
let ``the scope that came back is carried into the confirmation`` () =
    let fs = mockFs ()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    let state = loaded fs |> setCursor Left 1

    let result = update fs (DeleteScopeLoaded(WithDirectoryContents, Permanently)) state

    Assert.Equal<Effect list>(
        [ ConfirmDelete
              { Targets = [ en "docs" ]
                Scope = WithDirectoryContents
                Mode = Permanently } ],
        result.Effects
    )

[<Fact>]
let ``delete requested on the parent entry asks nothing`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (DeleteRequested ToTrash) state

    Assert.Equal(withHelpAt (DeleteRequested ToTrash) state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a confirmed delete hands the targets over with the mode it was asked for`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs (DeleteConfirmed ToTrash) state

    Assert.Equal<Effect list>([ DeletePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToTrash) ], result.Effects)
    Assert.True(fs.File.Exists(at "start/notes.txt"))

[<Fact>]
let ``a confirmed permanent delete carries its own mode`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs (DeleteConfirmed Permanently) state

    Assert.Equal<Effect list>([ DeletePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], Permanently) ], result.Effects)

[<Fact>]
let ``a wipe asks under its own mode`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs (DeleteRequested ByWiping) state

    Assert.Equal<Effect list>(
        [ InspectDelete(
              { Probed = [ fp (at "start/notes.txt") ]
                AtLeast = NamedEntriesOnly },
              ByWiping
          ) ],
        result.Effects
    )

[<Fact>]
let ``a confirmed wipe hands the targets over to be wiped`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs (DeleteConfirmed ByWiping) state

    Assert.Equal<Effect list>([ DeletePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ByWiping) ], result.Effects)
    Assert.True(fs.File.Exists(at "start/notes.txt"))

[<Fact>]
let ``a finished wipe keeps the marks of what it did not get to`` () =
    let fs = mockFs ()

    let state =
        loaded fs
        |> setCursor Left 1
        |> toggleSelectionMoving Downward Left
        |> toggleSelectionMoving Downward Left
        |> toggleSelectionMoving Downward Left

    let results =
        [ { Source = ItemOnDisk(fp (at "start/docs"))
            Outcome = Ok() }
          { Source = ItemOnDisk(fp (at "start/notes.txt"))
            Outcome = Error(Cancelled(fp (at "start/notes.txt"))) } ]

    let result = update fs (DeleteFinished results) state

    Assert.Equal<string list>([ "notes.txt"; "setup.fs" ], PanelState.selectedEntries result.State.Left |> names)

[<Fact>]
let ``a finished delete refreshes the panel and raises no effect`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2
    let results = deleteAll fs unattended [ fp (at "start/notes.txt") ]

    let result = update fs (DeleteFinished results) state

    Assert.False(fs.File.Exists(at "start/notes.txt"))
    Assert.DoesNotContain("notes.txt", names result.State.Left.Entries)
    Assert.Empty result.Effects

[<Fact>]
let ``a delete that fails is reported item by item`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2
    let results = deleteAll fs unattended [ fp (at "start/gone.txt") ]

    let result = update fs (DeleteFinished results) state

    Assert.Equal<Effect list>(
        [ Report(
              Problem.ItemFailures
                  [ { Source = ItemOnDisk(fp (at "start/gone.txt"))
                      Outcome = Error(SourceNotFound(fp (at "start/gone.txt"))) } ]
          ) ],
        result.Effects
    )
