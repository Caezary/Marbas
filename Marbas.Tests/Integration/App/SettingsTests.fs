module Marbas.Tests.Integration.App.SettingsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private settingsPath = fp (at "config/marbas.conf")

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "x")
    fs.AddFile(at "start/.profile", MockFileData "x")
    fs.AddDirectory(at "backup")
    fs

let private deps fs =
    { Fakes.dependencies fs (at "start") with
        SettingsPath = settingsPath }

let private store fs settings = Settings.save fs settingsPath settings

[<Fact>]
let ``what a session was set to is what the next one starts with`` () =
    let fs = mockFs ()

    use first =
        focusedWindow
            { deps fs with
                ChooseOption = fun _ _ -> Some (at "backup") }

    press (leftList first) KeyMap.sizeSortKey
    press (leftList first) KeyMap.hiddenKey
    press (leftList first) KeyMap.rightRootKey
    press (leftList first) KeyMap.quitKey

    use second = focusedWindow (deps fs)

    Assert.Equal(at "start" + " [size ↑ hidden]", panelTitle FilePanel.LeftId second)
    Assert.Equal(at "backup", shownPath FilePanel.RightId second)

[<Fact>]
let ``the panels start on the start path when nothing was ever saved`` () =
    let fs = mockFs ()

    use window = focusedWindow (deps fs)

    Assert.Equal(at "start" + " [name ↑]", panelTitle FilePanel.LeftId window)
    Assert.Equal(at "start" + " [name ↑]", panelTitle FilePanel.RightId window)

[<Fact>]
let ``a stored session puts each panel where it says`` () =
    let fs = mockFs ()

    store
        fs
        { Left =
            { Path = fp (at "backup")
              Sort = SortOrder.create ByDate Descending
              Hidden = Concealed }
          Right =
            { Path = fp (at "start")
              Sort = SortOrder.byName
              Hidden = Shown }
          LastCreatedDirectory = None
          Editors = EditorSettings.none }

    use window = focusedWindow (deps fs)

    Assert.Equal(at "backup" + " [date ↓]", panelTitle FilePanel.LeftId window)
    Assert.Equal(at "start" + " [name ↑ hidden]", panelTitle FilePanel.RightId window)

[<Fact>]
let ``a stored directory that is gone starts the panel on the start path without complaint`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()

    store
        fs
        { Left =
            { Path = fp (at "gone")
              Sort = SortOrder.create BySize Ascending
              Hidden = Concealed }
          Right = { Path = fp (at "start"); Sort = SortOrder.byName; Hidden = Concealed }
          LastCreatedDirectory = None
          Editors = EditorSettings.none }

    use window =
        focusedWindow
            { deps fs with
                ReportError = fun _ message -> reported.Add message }

    Assert.Equal(at "start" + " [size ↑]", panelTitle FilePanel.LeftId window)
    Assert.Empty reported

[<Fact>]
let ``a hand written settings file is read as far as it makes sense`` () =
    let fs = mockFs ()
    fs.AddFile(FileSystemPath.value settingsPath, MockFileData "left.sort=size\nleft.direction=nonsense\n# a note")

    use window = focusedWindow (deps fs)

    Assert.Equal(at "start" + " [size ↑]", panelTitle FilePanel.LeftId window)
    Assert.Equal(at "start" + " [name ↑]", panelTitle FilePanel.RightId window)
