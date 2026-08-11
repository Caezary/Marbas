module Marbas.Tests.Integration.SelectionKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")
    fs

[<Fact>]
let ``selection keys mark the cursor entry and move the cursor down`` () =
    for key in PanelInput.selectionKeys do
        use window = focusedWindow (Fakes.dependencies (mockFs ()) @"C:\start")
        let list = leftList window
        list.SelectedItem <- 1

        press list key

        Assert.True(list.Source.IsMarked 1, $"%A{key} should mark the cursor entry")
        Assert.Equal(2, list.SelectedItem.GetValueOrDefault 0)

[<Fact>]
let ``a selection key pressed twice leaves the entry unmarked`` () =
    for key in PanelInput.selectionKeys do
        use window = focusedWindow (Fakes.dependencies (mockFs ()) @"C:\start")
        let list = leftList window
        list.SelectedItem <- 1

        press list key
        list.SelectedItem <- 1
        press list key

        Assert.False(list.Source.IsMarked 1, $"%A{key} should unmark the cursor entry")

[<Fact>]
let ``the parent entry cannot be marked from the keyboard`` () =
    use window = focusedWindow (Fakes.dependencies (mockFs ()) @"C:\start")
    let list = leftList window

    press list PanelInput.selectionKeys.Head

    Assert.False(list.Source.IsMarked 0)
    Assert.Equal(1, list.SelectedItem.GetValueOrDefault 0)

[<Fact>]
let ``marks follow the panel they were made in`` () =
    use window = focusedWindow (Fakes.dependencies (mockFs ()) @"C:\start")
    let left = leftList window
    let right = rightList window
    left.SelectedItem <- 1

    press left PanelInput.selectionKeys.Head

    Assert.True(left.Source.IsMarked 1)
    Assert.False(right.Source.IsMarked 1)
