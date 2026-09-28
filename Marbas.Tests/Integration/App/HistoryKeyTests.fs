module Marbas.Tests.Integration.App.HistoryKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Ui.App
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddDirectory(at "elsewhere")
    fs

let private windowChoosing choose =
    focusedWindow
        { Fakes.dependencies (mockFs ()) (at "start") with
            Prompts.ChooseHistory = choose }

let private recording (asked: HistoryTab list ref) =
    fun (prompt: Ports.HistoryPrompt) ->
        asked.Value <- asked.Value @ [ prompt.Tab ]
        { Tab = prompt.Tab; Choice = None }

[<Fact>]
let ``Alt+F12 opens the history on the directories`` () =
    let asked = ref []
    use window = windowChoosing (recording asked)

    pressKey window KeyMap.historyKey

    Assert.Equal<HistoryTab list>([ FolderHistoryTab ], asked.Value)

[<Fact>]
let ``Alt+Shift+H opens the same history`` () =
    let asked = ref []
    use window = windowChoosing (recording asked)

    pressKey window KeyMap.spareHistoryKey

    Assert.Equal<HistoryTab list>([ FolderHistoryTab ], asked.Value)

[<Fact>]
let ``a directory chosen from the history is where the active panel stands`` () =
    use window =
        windowChoosing (fun prompt ->
            { Tab = prompt.Tab
              Choice = Some(OpenPlace(Place.OnDisk(FileSystemPath.create (at "elsewhere")))) })

    pressKey window KeyMap.historyKey

    Assert.Equal(at "elsewhere", shownPath FilePanel.LeftId window)
