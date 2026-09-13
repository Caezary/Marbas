module Marbas.Tests.Integration.App.SettingsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Collections
open Marbas.Domain
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

let private session left right : Settings =
    { Tabs =
        Zipper.single
            { Left = left
              Right = right
              Active = Left }
      LastCreatedDirectory = None
      Programs = ProgramSettings.none }

[<Fact>]
let ``what a session was set to is what the next one starts with`` () =
    let fs = mockFs ()

    use first =
        focusedWindow
            { deps fs with
                ChooseDrive = Fakes.choosingDrive (at "backup") }

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

let private savedTabs fs =
    Settings.load fs (Settings.forPath (fp (at "start"))) settingsPath
    |> _.Tabs
    |> Zipper.items

[<Fact>]
let ``opening a tab writes the session as it stands`` () =
    let fs = mockFs ()

    use window = focusedWindow (deps fs)
    press (leftList window) KeyMap.newTabKey

    Assert.Equal(2, List.length (savedTabs fs))

[<Fact>]
let ``closing a tab writes what is left of the session`` () =
    let fs = mockFs ()

    use window = focusedWindow (deps fs)
    press (leftList window) KeyMap.newTabKey
    press (leftList window) KeyMap.closeTabKey

    Assert.Equal(1, List.length (savedTabs fs))

[<Fact>]
let ``the last tab cannot be closed away`` () =
    let fs = mockFs ()

    use window = focusedWindow (deps fs)
    press (leftList window) KeyMap.closeTabKey

    Assert.Equal(1, List.length (savedTabs fs))
    Assert.Equal(at "start" + " [name ↑]", panelTitle FilePanel.LeftId window)

[<Fact>]
let ``the save key writes the session without quitting`` () =
    let fs = mockFs ()

    use window = focusedWindow (deps fs)
    press (leftList window) KeyMap.sizeSortKey
    press (leftList window) KeyMap.saveSessionKey

    Assert.Equal(BySize, (List.head (savedTabs fs)).Left.Sort.Key)
    Assert.Equal(at "start" + " [size ↑]", panelTitle FilePanel.LeftId window)

[<Fact>]
let ``a stored session of several tabs opens on the one it was left on`` () =
    let fs = mockFs ()

    let panelOn path =
        { Path = fp (at path)
          Sort = SortOrder.byName
          Hidden = Concealed
          Cursor = None }

    store
        fs
        { session (panelOn "start") (panelOn "start") with
            Tabs =
                Zipper.ofNonEmpty
                    { Left = panelOn "start"
                      Right = panelOn "start"
                      Active = Left }
                    [ { Left = panelOn "backup"
                        Right = panelOn "start"
                        Active = Left } ]
                |> Zipper.at 1 }

    use window = focusedWindow (deps fs)

    Assert.Equal(at "backup" + " [name ↑]", panelTitle FilePanel.LeftId window)

[<Fact>]
let ``a stored session puts each panel where it says`` () =
    let fs = mockFs ()

    store
        fs
        (session
            { Path = fp (at "backup")
              Sort = SortOrder.create ByDate Descending
              Hidden = Concealed
              Cursor = None }
            { Path = fp (at "start")
              Sort = SortOrder.byName
              Hidden = Shown
              Cursor = None })

    use window = focusedWindow (deps fs)

    Assert.Equal(at "backup" + " [date ↓]", panelTitle FilePanel.LeftId window)
    Assert.Equal(at "start" + " [name ↑ hidden]", panelTitle FilePanel.RightId window)

[<Fact>]
let ``a stored directory that is gone starts the panel on the start path without complaint`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()

    store
        fs
        (session
            { Path = fp (at "gone")
              Sort = SortOrder.create BySize Ascending
              Hidden = Concealed
              Cursor = None }
            { Path = fp (at "start")
              Sort = SortOrder.byName
              Hidden = Concealed
              Cursor = None })

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

[<Fact>]
let ``a session remembers where each cursor stood`` () =
    let fs = mockFs ()

    use first = focusedWindow (deps fs)
    press (leftList first) Terminal.Gui.Input.Key.CursorDown
    press (leftList first) KeyMap.quitKey

    use second = focusedWindow (deps fs)

    Assert.Equal(1, (leftList second).SelectedItem.GetValueOrDefault -1)
    Assert.Equal(0, (rightList second).SelectedItem.GetValueOrDefault -1)

[<Fact>]
let ``a tab that has not been read yet still comes back to its cursor`` () =
    let fs = mockFs ()

    let panelOn cursor =
        { Path = fp (at "start")
          Sort = SortOrder.byName
          Hidden = Concealed
          Cursor = cursor }

    let tabOn cursor =
        { Left = panelOn cursor
          Right = panelOn None
          Active = Left }

    store
        fs
        { session (panelOn None) (panelOn None) with
            Tabs = Zipper.ofNonEmpty (tabOn None) [ tabOn (Some(EntryName.create "notes.txt")) ] }

    use window = focusedWindow (deps fs)

    Assert.Equal(0, (leftList window).SelectedItem.GetValueOrDefault -1)

    press (leftList window) KeyMap.nextTabKey

    Assert.Equal(1, (leftList window).SelectedItem.GetValueOrDefault -1)
