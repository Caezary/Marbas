module Marbas.Tests.Integration.Panels.PanelPaintingTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Drawing
open Terminal.Gui.Views
open Marbas.Ui.Theme
open Marbas.Ui.Keys
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs.AddFile(at "start/setup.exe", MockFileData "hi")
    fs.AddFile(at "start/sources.zip", MockFileData "hi")
    fs

let private window () = focusedWindow (Fakes.dependencies (mockFs ()) (at "start"))

let private paintedRow (list: ListView) row =
    let args = ListViewRowEventArgs row
    list.OnRowRender args
    args.RowAttribute

[<Fact>]
let ``every kind of entry is painted with its own colour`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 0

    let painted = [ 1..4 ] |> List.map (paintedRow list)

    Assert.Equal<Nullable<Attribute> list>(
        [ Nullable ClassicScheme.directoryEntry
          Nullable ClassicScheme.plainEntry
          Nullable ClassicScheme.executableEntry
          Nullable ClassicScheme.archiveEntry ],
        painted
    )

[<Fact>]
let ``a link is painted with the link colour`` () =
    let fs = mockFs ()
    fs.Directory.CreateSymbolicLink(at "start/twin", at "start/docs") |> ignore
    use window = focusedWindow (Fakes.dependencies fs (at "start"))
    let list = leftList window
    list.SelectedItem <- 0

    let painted = paintedRow list 2

    Assert.Equal(Nullable ClassicScheme.linkEntry, painted)

[<Fact>]
let ``the parent entry is painted like a directory`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 1

    let painted = paintedRow list 0

    Assert.Equal(Nullable ClassicScheme.directoryEntry, painted)

[<Fact>]
let ``a marked entry keeps the colour of a mark`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 1
    press list KeyMap.selectionKeys.Head

    let painted = paintedRow list 1

    Assert.False painted.HasValue

[<Fact>]
let ``the entry under the cursor keeps the colour of the cursor`` () =
    use window = window ()
    let list = leftList window
    list.SelectedItem <- 2

    let painted = paintedRow list 2

    Assert.True(list.HasFocus, "the focused panel's list should hold the focus")
    Assert.False painted.HasValue

[<Fact>]
let ``the cursor of an unfocused panel gives its row back to the colours`` () =
    use window = window ()
    let list = rightList window
    list.SelectedItem <- 2

    let painted = paintedRow list 2

    Assert.Equal(Nullable ClassicScheme.plainEntry, painted)
