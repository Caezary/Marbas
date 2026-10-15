module Marbas.Tests.State.SelectionTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.State
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
    PanelState.selectedEntries panel |> List.map (_.Name >> EntryName.value)

let private startedWith fileCount =
    let fs = MockFileSystem()

    for number in 1..fileCount do
        fs.AddFile(at $"start/file{number:D2}.txt", MockFileData "hi")

    create (fp (at "start")) |> loaded Left (fp (at "start")) fs

[<Fact>]
let ``a fresh panel has nothing selected`` () =
    let _, state = started ()

    Assert.Empty(PanelState.selectedEntries state.Left)

[<Fact>]
let ``toggling selects the entry under the cursor and advances the cursor`` () =
    let _, state = started ()

    let updated = state |> setCursor Left 1 |> toggleSelectionMoving Downward Left

    Assert.Equal<string list>([ "docs" ], selectedNames updated.Left)
    Assert.Equal(2, updated.Left.Cursor)

[<Fact>]
let ``toggling an already selected entry deselects it`` () =
    let _, state = started ()

    let updated =
        state
        |> setCursor Left 1
        |> toggleSelectionMoving Downward Left
        |> setCursor Left 1
        |> toggleSelectionMoving Downward Left

    Assert.Empty(PanelState.selectedEntries updated.Left)

[<Fact>]
let ``selecting several entries keeps them in listing order`` () =
    let _, state = started ()

    let updated =
        state
        |> setCursor Left 3
        |> toggleSelectionMoving Downward Left
        |> setCursor Left 1
        |> toggleSelectionMoving Downward Left

    Assert.Equal<string list>([ "docs"; "readme.txt" ], selectedNames updated.Left)

[<Fact>]
let ``the parent entry is never selectable but the cursor still advances`` () =
    let _, state = started ()

    let updated = toggleSelectionMoving Downward Left state

    Assert.Empty(PanelState.selectedEntries updated.Left)
    Assert.Equal(1, updated.Left.Cursor)

[<Fact>]
let ``the cursor stops at the last entry`` () =
    let _, state = started ()

    let updated = state |> setCursor Left 3 |> toggleSelectionMoving Downward Left

    Assert.Equal(3, updated.Left.Cursor)
    Assert.Equal<string list>([ "readme.txt" ], selectedNames updated.Left)

[<Fact>]
let ``marking upward selects the entry under the cursor and moves the cursor up`` () =
    let _, state = started ()

    let updated = state |> setCursor Left 2 |> toggleSelectionMoving Upward Left

    Assert.Equal<string list>([ "notes.txt" ], selectedNames updated.Left)
    Assert.Equal(1, updated.Left.Cursor)

[<Fact>]
let ``marking upward over a selected entry unmarks it`` () =
    let _, state = started ()

    let updated =
        state
        |> setCursor Left 3
        |> toggleSelectionMoving Downward Left
        |> setCursor Left 3
        |> toggleSelectionMoving Upward Left

    Assert.Empty(PanelState.selectedEntries updated.Left)
    Assert.Equal(2, updated.Left.Cursor)

[<Fact>]
let ``marking a run upward leaves every entry it passed selected`` () =
    let _, state = started ()

    let updated =
        state
        |> setCursor Left 3
        |> toggleSelectionMoving Upward Left
        |> toggleSelectionMoving Upward Left
        |> toggleSelectionMoving Upward Left

    Assert.Equal<string list>([ "docs"; "notes.txt"; "readme.txt" ], selectedNames updated.Left)

[<Fact>]
let ``the cursor stops at the first entry`` () =
    let _, state = started ()

    let updated = state |> setCursor Left 1 |> toggleSelectionMoving Upward Left

    Assert.Equal(0, updated.Left.Cursor)
    Assert.Equal<string list>([ "docs" ], selectedNames updated.Left)

[<Fact>]
let ``marking through an entry stops short of it, the way a single step does`` () =
    let _, state = started ()

    let updated = state |> setCursor Left 1 |> toggleSelectionShortOf Left 3

    Assert.Equal<string list>([ "docs"; "notes.txt" ], selectedNames updated.Left)
    Assert.Equal(3, updated.Left.Cursor)

[<Fact>]
let ``marking back through an entry stops short of it too`` () =
    let _, state = started ()

    let updated = state |> setCursor Left 3 |> toggleSelectionShortOf Left 1

    Assert.Equal<string list>([ "notes.txt"; "readme.txt" ], selectedNames updated.Left)
    Assert.Equal(1, updated.Left.Cursor)

[<Fact>]
let ``a run never takes the parent entry it passes over`` () =
    let _, state = started ()

    let updated = toggleSelectionShortOf Left 3 state

    Assert.Equal<string list>([ "docs"; "notes.txt" ], selectedNames updated.Left)

[<Fact>]
let ``a run that cannot move on marks the entry it stands on`` () =
    let _, state = started ()

    let updated = state |> setCursor Left 3 |> toggleSelectionShortOf Left 3

    Assert.Equal<string list>([ "readme.txt" ], selectedNames updated.Left)
    Assert.Equal(3, updated.Left.Cursor)

[<Fact>]
let ``one run after another marks page by page and never gives the last one back`` () =
    let state = startedWith 11
    let pageAfterPage = [ 4; 9; 11; 11 ]

    let updated =
        pageAfterPage
        |> List.fold (fun state target -> toggleSelectionShortOf Left target state) state

    Assert.Equal(11, (PanelState.selectedEntries updated.Left).Length)

[<Fact>]
let ``an unmarked entry under the cursor marks the whole run, whatever it holds`` () =
    let _, state = started ()

    let updated =
        state
        |> setCursor Left 3
        |> toggleSelectionMoving Downward Left
        |> setCursor Left 1
        |> toggleSelectionShortOf Left 3

    Assert.Equal<string list>([ "docs"; "notes.txt"; "readme.txt" ], selectedNames updated.Left)

[<Fact>]
let ``a marked entry under the cursor clears the whole run, whatever it holds`` () =
    let _, state = started ()

    let updated =
        state
        |> setCursor Left 2
        |> toggleSelectionMoving Downward Left
        |> setCursor Left 2
        |> toggleSelectionShortOf Left 3

    Assert.Empty(PanelState.selectedEntries updated.Left)
    Assert.Equal(3, updated.Left.Cursor)

[<Fact>]
let ``a run that reaches past the last entry stops at it`` () =
    let _, state = started ()

    let updated = state |> setCursor Left 2 |> toggleSelectionShortOf Left 9

    Assert.Equal<string list>([ "notes.txt"; "readme.txt" ], selectedNames updated.Left)
    Assert.Equal(3, updated.Left.Cursor)

[<Fact>]
let ``marking undoes one entry of a pattern selection at a time`` () =
    let _, state = started ()

    let updated =
        state
        |> applyPattern Select Left (NamePattern.tryCreate "*.txt" |> Expect.some)
        |> setCursor Left 2
        |> toggleSelectionMoving Downward Left

    Assert.Equal<string list>([ "readme.txt" ], selectedNames updated.Left)

[<Fact>]
let ``selection belongs to one panel only`` () =
    let fs = mockFs ()

    let state =
        create (fp (at "start"))
        |> loaded Left (fp (at "start")) fs
        |> loaded Right (fp (at "start")) fs

    let updated = state |> setCursor Left 1 |> toggleSelectionMoving Downward Left

    Assert.Equal<string list>([ "docs" ], selectedNames updated.Left)
    Assert.Empty(PanelState.selectedEntries updated.Right)

[<Fact>]
let ``entering a directory clears the selection`` () =
    let fs, state = started ()

    let selected = state |> setCursor Left 2 |> toggleSelectionMoving Downward Left
    let updated = selected |> setCursor Left 1 |> enter fs |> Expect.ok

    Assert.Equal(OnDisk(fp (at "start/docs")), updated.Left.Path)
    Assert.Empty(PanelState.selectedEntries updated.Left)

[<Fact>]
let ``refresh keeps the selection and drops entries that disappeared`` () =
    let fs, state = started ()

    let selected =
        state
        |> setCursor Left 2
        |> toggleSelectionMoving Downward Left
        |> toggleSelectionMoving Downward Left

    fs.File.Delete(at "start/notes.txt")

    let updated = refresh fs selected |> Expect.ok

    Assert.Equal<string list>([ "notes.txt"; "readme.txt" ], selectedNames selected.Left)
    Assert.Equal<string list>([ "readme.txt" ], selectedNames updated.Left)
