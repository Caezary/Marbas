module Marbas.Tests.Integration.SettingsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private fp = FileSystemPath.create

let private settingsPath = fp @"C:\config\marbas.conf"

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\start\notes.txt", MockFileData "x")
    fs.AddFile(@"C:\start\.profile", MockFileData "x")
    fs.AddDirectory @"C:\backup"
    fs

let private deps fs =
    { Fakes.dependencies fs @"C:\start" with
        SettingsPath = settingsPath }

let private store fs settings = Settings.save fs settingsPath settings

[<Fact>]
let ``what a session was set to is what the next one starts with`` () =
    let fs = mockFs ()

    use first =
        focusedWindow
            { deps fs with
                ChooseOption = fun _ _ -> Some @"C:\backup" }

    press (leftList first) KeyMap.sizeSortKey
    press (leftList first) KeyMap.hiddenKey
    press (leftList first) KeyMap.rightDriveKey
    press (leftList first) KeyMap.quitKey

    use second = focusedWindow (deps fs)

    Assert.Equal(@"C:\start [size ↑ hidden]", panelTitle FilePanel.LeftId second)
    Assert.Equal(@"C:\backup", shownPath FilePanel.RightId second)

[<Fact>]
let ``the panels start on the start path when nothing was ever saved`` () =
    let fs = mockFs ()

    use window = focusedWindow (deps fs)

    Assert.Equal(@"C:\start [name ↑]", panelTitle FilePanel.LeftId window)
    Assert.Equal(@"C:\start [name ↑]", panelTitle FilePanel.RightId window)

[<Fact>]
let ``a stored session puts each panel where it says`` () =
    let fs = mockFs ()

    store
        fs
        { Left =
            { Path = fp @"C:\backup"
              Sort = SortOrder.create ByDate Descending
              Hidden = Concealed }
          Right =
            { Path = fp @"C:\start"
              Sort = SortOrder.byName
              Hidden = Shown } }

    use window = focusedWindow (deps fs)

    Assert.Equal(@"C:\backup [date ↓]", panelTitle FilePanel.LeftId window)
    Assert.Equal(@"C:\start [name ↑ hidden]", panelTitle FilePanel.RightId window)

[<Fact>]
let ``a stored directory that is gone starts the panel on the start path without complaint`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()

    store
        fs
        { Left =
            { Path = fp @"C:\gone"
              Sort = SortOrder.create BySize Ascending
              Hidden = Concealed }
          Right = { Path = fp @"C:\start"; Sort = SortOrder.byName; Hidden = Concealed } }

    use window =
        focusedWindow
            { deps fs with
                ReportError = fun _ message -> reported.Add message }

    Assert.Equal(@"C:\start [size ↑]", panelTitle FilePanel.LeftId window)
    Assert.Empty reported

[<Fact>]
let ``a hand written settings file is read as far as it makes sense`` () =
    let fs = mockFs ()
    fs.AddFile(FileSystemPath.value settingsPath, MockFileData "left.sort=size\nleft.direction=nonsense\n# a note")

    use window = focusedWindow (deps fs)

    Assert.Equal(@"C:\start [size ↑]", panelTitle FilePanel.LeftId window)
    Assert.Equal(@"C:\start [name ↑]", panelTitle FilePanel.RightId window)
