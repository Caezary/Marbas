module Marbas.Tests.Integration.SelectionKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs

[<Fact>]
let ``selection keys mark the cursor entry and move the cursor down`` () =
    for key in KeyMap.selectionKeys do
        use window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))
        let list = leftList window
        list.SelectedItem <- 1

        press list key

        Assert.True(list.Source.IsMarked 1, $"%A{key} should mark the cursor entry")
        Assert.Equal(2, list.SelectedItem.GetValueOrDefault 0)

[<Fact>]
let ``a selection key pressed twice leaves the entry unmarked`` () =
    for key in KeyMap.selectionKeys do
        use window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))
        let list = leftList window
        list.SelectedItem <- 1

        press list key
        list.SelectedItem <- 1
        press list key

        Assert.False(list.Source.IsMarked 1, $"%A{key} should unmark the cursor entry")

[<Fact>]
let ``the parent entry cannot be marked from the keyboard`` () =
    use window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))
    let list = leftList window

    press list KeyMap.selectionKeys.Head

    Assert.False(list.Source.IsMarked 0)
    Assert.Equal(1, list.SelectedItem.GetValueOrDefault 0)

[<Fact>]
let ``holding shift marks a run of entries on the way up`` () =
    use window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))
    let list = leftList window
    list.SelectedItem <- 3

    press list KeyMap.selectionUpKey
    press list KeyMap.selectionUpKey

    Assert.True(list.Source.IsMarked 3)
    Assert.True(list.Source.IsMarked 2)
    Assert.Equal(1, list.SelectedItem.GetValueOrDefault 0)

[<Fact>]
let ``holding shift back over a marked run unmarks it`` () =
    use window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))
    let list = leftList window
    list.SelectedItem <- 1

    press list KeyMap.selectionDownKey
    press list KeyMap.selectionDownKey
    list.SelectedItem <- 2
    press list KeyMap.selectionUpKey
    press list KeyMap.selectionUpKey

    Assert.False(list.Source.IsMarked 1)
    Assert.False(list.Source.IsMarked 2)
    Assert.Equal(0, list.SelectedItem.GetValueOrDefault 0)

[<Fact>]
let ``a shift arrow and a pattern add up to one selection`` () =
    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                AskPattern = fun _ -> Some "*.txt" }

    let list = leftList window
    list.SelectedItem <- 1

    press list KeyMap.selectionDownKey
    press list KeyMap.selectPatternKey

    Assert.True(list.Source.IsMarked 1, "the shift arrow mark should survive the pattern")
    Assert.True(list.Source.IsMarked 2)
    Assert.True(list.Source.IsMarked 3)

[<Fact>]
let ``a shift arrow unmarks a single entry a pattern selected`` () =
    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                AskPattern = fun _ -> Some "*.txt" }

    let list = leftList window

    press list KeyMap.selectPatternKey
    list.SelectedItem <- 2
    press list KeyMap.selectionDownKey

    Assert.False(list.Source.IsMarked 2)
    Assert.True(list.Source.IsMarked 3)

[<Fact>]
let ``marks follow the panel they were made in`` () =
    use window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))
    let left = leftList window
    let right = rightList window
    left.SelectedItem <- 1

    press left KeyMap.selectionKeys.Head

    Assert.True(left.Source.IsMarked 1)
    Assert.False(right.Source.IsMarked 1)
