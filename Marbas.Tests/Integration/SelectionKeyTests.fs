module Marbas.Tests.Integration.SelectionKeyTests

open System.Drawing
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

let private laidOutWindow () =
    let window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))
    window.Layout(Size(80, 24)) |> ignore
    window

[<Fact>]
let ``a shift page key marks up to the entry the cursor lands on, not including it`` () =
    use window = laidOutWindow ()
    let list = leftList window
    list.SelectedItem <- 1

    press list KeyMap.selectionPageDownKeys.Head

    Assert.Equal<bool list>([ true; true; false ], [ for i in 1..3 -> list.Source.IsMarked i ])
    Assert.Equal(3, list.SelectedItem.GetValueOrDefault -1)

[<Fact>]
let ``a shift page key over a half marked page goes by the entry under the cursor`` () =
    use window = laidOutWindow ()
    let list = leftList window
    list.SelectedItem <- 2
    press list KeyMap.selectionDownKey
    list.SelectedItem <- 1

    press list KeyMap.selectionPageDownKeys.Head

    Assert.Equal<bool list>([ true; true ], [ for i in 1..2 -> list.Source.IsMarked i ])

[<Fact>]
let ``pressing a shift page key on and on marks the whole listing`` () =
    let fs = MockFileSystem()

    for number in 1..40 do
        fs.AddFile(at $"start/file{number:D2}.txt", MockFileData "hi")

    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    window.Layout(Size(80, 24)) |> ignore
    let list = leftList window

    for _ in 1..12 do
        press list KeyMap.selectionPageDownKeys.Head

    Assert.Equal<bool list>(List.replicate 40 true, [ for i in 1..40 -> list.Source.IsMarked i ])

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
