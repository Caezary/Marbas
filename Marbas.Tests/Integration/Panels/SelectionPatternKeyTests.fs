module Marbas.Tests.Integration.Panels.SelectionPatternKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.Ui.Keys
open Marbas.Ui.Panels
open Marbas.Ui.App
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.TXT", MockFileData "hi")
    fs.AddFile(at "start/setup.fs", MockFileData "hi")
    fs

let private windowWith askPattern =
    focusedWindow
        { Fakes.dependencies (mockFs ()) (at "start") with
            Prompts.AskPattern = askPattern }

let private marked (list: ListView) =
    [ for i in 0 .. list.Source.Count - 1 do
          if list.Source.IsMarked i then
              string (list.Source.ToList()[i]) ]

[<Fact>]
let ``the select key asks for a pattern and marks the matches`` () =
    let asked = ResizeArray<string>()

    use window =
        windowWith (fun request ->
            asked.Add request.Title
            Some "*.fs")

    let list = leftList window

    press list KeyMap.selectPatternKey

    Assert.Equal<string list>([ Shell.SelectPrompt ], List.ofSeq asked)
    Assert.Collection(marked list, fun row -> Assert.StartsWith("setup.fs", row))

[<Fact>]
let ``the deselect key asks for a pattern and unmarks the matches`` () =
    let asked = ResizeArray<string>()

    use window =
        windowWith (fun request ->
            asked.Add request.Title
            Some "*.txt")

    let list = leftList window
    press list KeyMap.selectPatternKey
    let markedBefore = marked list

    press list KeyMap.deselectPatternKey

    Assert.Equal<string list>([ Shell.SelectPrompt; Shell.DeselectPrompt ], List.ofSeq asked)
    Assert.Equal(2, markedBefore.Length)
    Assert.Empty(marked list)

[<Fact>]
let ``cancelling the pattern prompt changes nothing`` () =
    use window = windowWith (fun _ -> None)
    let list = leftList window
    list.SelectedItem <- 1
    press list KeyMap.selectionKeys.Head

    press list KeyMap.selectPatternKey

    Assert.Collection(marked list, fun row -> Assert.StartsWith("docs", row))

[<Fact>]
let ``the panel shows a summary of what is selected`` () =
    use window = windowWith (fun _ -> Some "*.txt")
    let panelView = panel FilePanel.LeftId window
    let footerBefore = (FilePanel.footer panelView).Text

    press (FilePanel.listView panelView) KeyMap.selectPatternKey

    Assert.Contains("3 files", footerBefore)
    Assert.Contains("2 selected", (FilePanel.footer panelView).Text)
    Assert.Contains("4 bytes", (FilePanel.footer panelView).Text)
