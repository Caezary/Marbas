module Marbas.Tests.Integration.Panels.UpOneLevelKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Drivers
open Terminal.Gui.Input
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "hi")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs

[<Theory>]
[<InlineData(KeyCode.PageUp ||| KeyCode.CtrlMask)>]
[<InlineData(KeyCode.U ||| KeyCode.AltMask)>]
let ``the up keys take the panel to the parent directory`` (up: KeyCode) =
    use window = focusedWindow (Fakes.dependencies (mockFs ()) (at "start/docs"))
    let list = leftList window
    list.SelectedItem <- 1

    press list (Key up)

    Assert.Equal(at "start", shownPath FilePanel.LeftId window)
