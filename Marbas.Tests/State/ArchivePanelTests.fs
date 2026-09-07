module Marbas.Tests.State.ArchivePanelTests

open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () = packed ()

let private names panel = panel.Entries |> List.map (_.Name >> EntryName.value)

[<Fact>]
let ``the targets of a panel inside an archive have no paths on disk`` () =
    let fs = mockFs ()
    let state = standingInside fs Left |> setCursor Left 2

    let paths = operationPaths fs (activePanel state)

    Assert.Empty paths

[<Fact>]
let ``a file is not made inside an archive`` () =
    let fs = mockFs ()
    let state = standingInside fs Left

    let result = update fs NewFileRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``packing from a panel inside an archive asks for nothing`` () =
    let fs = mockFs ()
    let state = standingInside fs Left |> setCursor Left 2

    let result = update fs PackRequested state

    Assert.Empty result.Effects

[<Fact>]
let ``an entry inside an archive refuses to be edited`` () =
    let fs = mockFs ()
    let state = standingInside fs Left |> setCursor Left 2

    let result = update fs EditRequested state

    Assert.Equal<Effect list>(
        [ ReportOperationError(Operations.ArchiveEntryNotEditable(fp (at "start/backup.zip/notes.txt"))) ],
        result.Effects
    )

[<Fact>]
let ``the way out of an archive is not an entry to edit`` () =
    let fs = mockFs ()
    let state = standingInside fs Left

    let result = update fs EditRequested state

    Assert.Equal<string list>([ ".."; "docs"; "notes.txt" ], names (activePanel state))
    Assert.Empty result.Effects
