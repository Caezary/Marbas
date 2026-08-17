module Marbas.Tests.State.SelectionTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs

let private loaded side path (fs: IFileSystem) state =
    loadPanel fs side path state |> Expect.ok

let private started () =
    let fs = mockFs ()
    fs, create (fp (at "start")) |> loaded Left (fp (at "start")) fs

let private selectedNames panel =
    selectedEntries panel |> List.map (_.Name >> EntryName.value)

[<Fact>]
let ``a fresh panel has nothing selected`` () =
    let _, state = started ()

    Assert.Empty(selectedEntries state.Left)

[<Fact>]
let ``toggling selects the entry under the cursor and advances the cursor`` () =
    let _, state = started ()

    let updated = state |> setCursor Left 1 |> toggleSelectionAndAdvance Left

    Assert.Equal<string list>([ "docs" ], selectedNames updated.Left)
    Assert.Equal(2, updated.Left.Cursor)

[<Fact>]
let ``toggling an already selected entry deselects it`` () =
    let _, state = started ()

    let updated =
        state
        |> setCursor Left 1
        |> toggleSelectionAndAdvance Left
        |> setCursor Left 1
        |> toggleSelectionAndAdvance Left

    Assert.Empty(selectedEntries updated.Left)

[<Fact>]
let ``selecting several entries keeps them in listing order`` () =
    let _, state = started ()

    let updated =
        state
        |> setCursor Left 3
        |> toggleSelectionAndAdvance Left
        |> setCursor Left 1
        |> toggleSelectionAndAdvance Left

    Assert.Equal<string list>([ "docs"; "readme.txt" ], selectedNames updated.Left)

[<Fact>]
let ``the parent entry is never selectable but the cursor still advances`` () =
    let _, state = started ()

    let updated = toggleSelectionAndAdvance Left state

    Assert.Empty(selectedEntries updated.Left)
    Assert.Equal(1, updated.Left.Cursor)

[<Fact>]
let ``the cursor stops at the last entry`` () =
    let _, state = started ()

    let updated = state |> setCursor Left 3 |> toggleSelectionAndAdvance Left

    Assert.Equal(3, updated.Left.Cursor)
    Assert.Equal<string list>([ "readme.txt" ], selectedNames updated.Left)

[<Fact>]
let ``selection belongs to one panel only`` () =
    let fs = mockFs ()
    let state = create (fp (at "start")) |> loaded Left (fp (at "start")) fs |> loaded Right (fp (at "start")) fs

    let updated = state |> setCursor Left 1 |> toggleSelectionAndAdvance Left

    Assert.Equal<string list>([ "docs" ], selectedNames updated.Left)
    Assert.Empty(selectedEntries updated.Right)

[<Fact>]
let ``entering a directory clears the selection`` () =
    let fs, state = started ()

    let selected = state |> setCursor Left 2 |> toggleSelectionAndAdvance Left
    let updated = selected |> setCursor Left 1 |> enter fs |> Expect.ok

    Assert.Equal(fp (at "start/docs"), updated.Left.Path)
    Assert.Empty(selectedEntries updated.Left)

[<Fact>]
let ``refresh keeps the selection and drops entries that disappeared`` () =
    let fs, state = started ()

    let selected =
        state
        |> setCursor Left 2
        |> toggleSelectionAndAdvance Left
        |> toggleSelectionAndAdvance Left

    fs.File.Delete(at "start/notes.txt")

    let updated = refresh fs selected |> Expect.ok

    Assert.Equal<string list>([ "notes.txt"; "readme.txt" ], selectedNames selected.Left)
    Assert.Equal<string list>([ "readme.txt" ], selectedNames updated.Left)
