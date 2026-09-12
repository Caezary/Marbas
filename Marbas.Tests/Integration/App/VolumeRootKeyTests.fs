module Marbas.Tests.Integration.App.VolumeRootKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/notes.txt", MockFileData "x")
    fs

[<Fact>]
let ``the key a terminal makes of Ctrl and a backslash sends the panel to the root`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start/docs"))

    press (leftList window) KeyMap.volumeRootKey

    Assert.Equal(at "", shownPath FilePanel.LeftId window)

[<Fact>]
let ``the backslash spelled out sends the panel to the same place`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start/docs"))

    press (leftList window) KeyMap.classicVolumeRootKey

    Assert.Equal(at "", shownPath FilePanel.LeftId window)

[<Fact>]
let ``the panel the key was not pressed in stays where it stands`` () =
    let fs = mockFs ()
    use window = focusedWindow (Fakes.dependencies fs (at "start/docs"))

    press (leftList window) KeyMap.volumeRootKey

    Assert.Equal(at "start/docs", shownPath FilePanel.RightId window)
