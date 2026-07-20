module Marbas.Tests.SelectionTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Ui
open Marbas.State.AppState
open Marbas.Tests.TestHelpers

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")
    fs

let private loaded side path (fs: IFileSystem) state =
    match loadPanel fs side path state with
    | Ok s -> s
    | Error err -> failwith $"setup failed: %A{err}"

let private started () =
    let fs = mockFs ()
    fs, create @"C:\start" |> loaded Left @"C:\start" fs

let private selectedNames panel =
    selectedEntries panel |> List.map (fun e -> e.Name)

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

    let updated = state |> toggleSelectionAndAdvance Left

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

    let state =
        create @"C:\start"
        |> loaded Left @"C:\start" fs
        |> loaded Right @"C:\start" fs
        |> setCursor Left 1
        |> toggleSelectionAndAdvance Left

    Assert.Equal<string list>([ "docs" ], selectedNames state.Left)
    Assert.Empty(selectedEntries state.Right)

[<Fact>]
let ``entering a directory clears the selection`` () =
    let fs = mockFs ()
    let _, state = started ()

    let selected = state |> setCursor Left 2 |> toggleSelectionAndAdvance Left

    match selected |> setCursor Left 1 |> enter fs with
    | Ok updated ->
        Assert.Equal(@"C:\start\docs", updated.Left.Path)
        Assert.Empty(selectedEntries updated.Left)
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``refresh keeps the selection and drops entries that disappeared`` () =
    let fs = mockFs ()
    let _, state = started ()

    let selected =
        state
        |> setCursor Left 2
        |> toggleSelectionAndAdvance Left
        |> toggleSelectionAndAdvance Left

    Assert.Equal<string list>([ "notes.txt"; "readme.txt" ], selectedNames selected.Left)
    fs.File.Delete @"C:\start\notes.txt"

    match refresh fs selected with
    | Ok updated -> Assert.Equal<string list>([ "readme.txt" ], selectedNames updated.Left)
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``selection keys mark the cursor entry and move the cursor down`` () =
    for key in MainWindow.selectionKeys do
        use window = MainWindow.create (dependencies (mockFs ()) @"C:\start")
        window.BeginInit()
        window.EndInit()
        window.SetFocus() |> ignore

        let list = FilePanel.listView (panel FilePanel.LeftId window)
        list.SelectedItem <- 1
        press list key

        Assert.True(list.Source.IsMarked 1, $"%A{key} should mark the cursor entry")
        Assert.Equal(2, list.SelectedItem.GetValueOrDefault 0)

[<Fact>]
let ``a selection key pressed twice leaves the entry unmarked`` () =
    for key in MainWindow.selectionKeys do
        use window = MainWindow.create (dependencies (mockFs ()) @"C:\start")
        window.BeginInit()
        window.EndInit()
        window.SetFocus() |> ignore

        let list = FilePanel.listView (panel FilePanel.LeftId window)
        list.SelectedItem <- 1
        press list key
        list.SelectedItem <- 1
        press list key

        Assert.False(list.Source.IsMarked 1, $"%A{key} should unmark the cursor entry")

[<Fact>]
let ``the parent entry cannot be marked from the keyboard`` () =
    use window = MainWindow.create (dependencies (mockFs ()) @"C:\start")
    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    press list MainWindow.selectionKeys.Head

    Assert.False(list.Source.IsMarked 0)
    Assert.Equal(1, list.SelectedItem.GetValueOrDefault 0)

[<Fact>]
let ``marks follow the panel they were made in`` () =
    use window = MainWindow.create (dependencies (mockFs ()) @"C:\start")
    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore

    let left = FilePanel.listView (panel FilePanel.LeftId window)
    let right = FilePanel.listView (panel FilePanel.RightId window)
    left.SelectedItem <- 1
    press left MainWindow.selectionKeys.Head

    Assert.True(left.Source.IsMarked 1)
    Assert.False(right.Source.IsMarked 1)
