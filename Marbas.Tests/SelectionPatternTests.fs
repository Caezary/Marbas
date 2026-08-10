module Marbas.Tests.SelectionPatternTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui
open Marbas.State.AppState
open Marbas.Tests.TestHelpers

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs.AddFile(@"C:\start\readme.TXT", MockFileData "hi")
    fs.AddFile(@"C:\start\setup.fs", MockFileData "hi")
    fs

let private loaded (fs: IFileSystem) =
    create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") |> Expect.ok

let private selectedNames panel = selectedEntries panel |> List.map _.Name

[<Fact>]
let ``a pattern selects every matching entry in one step`` () =
    let state = loaded (mockFs ())

    let selected = selectMatching Left "*.fs" state

    Assert.Equal<string list>([ "setup.fs" ], selectedNames selected.Left)

[<Fact>]
let ``pattern matching ignores case`` () =
    let state = loaded (mockFs ())

    let selected = selectMatching Left "*.txt" state

    Assert.Equal<string list>([ "notes.txt"; "readme.TXT" ], selectedNames selected.Left)

[<Fact>]
let ``a pattern never selects the parent entry`` () =
    let state = loaded (mockFs ())

    let selected = selectMatching Left "*" state

    Assert.Equal<string list>([ "docs"; "notes.txt"; "readme.TXT"; "setup.fs" ], selectedNames selected.Left)

[<Fact>]
let ``a pattern adds to what is already selected`` () =
    let state = loaded (mockFs ())

    let selected = state |> selectMatching Left "*.fs" |> selectMatching Left "*.txt"

    Assert.Equal<string list>([ "notes.txt"; "readme.TXT"; "setup.fs" ], selectedNames selected.Left)

[<Fact>]
let ``a pattern deselects only the entries it matches`` () =
    let state = loaded (mockFs ())

    let selected = state |> selectMatching Left "*" |> deselectMatching Left "*.txt"

    Assert.Equal<string list>([ "docs"; "setup.fs" ], selectedNames selected.Left)

[<Fact>]
let ``a pattern that matches nothing leaves the selection alone`` () =
    let state = loaded (mockFs ())

    let selected =
        state
        |> selectMatching Left "*.fs"
        |> selectMatching Left "*.md"
        |> deselectMatching Left "*.md"

    Assert.Equal<string list>([ "setup.fs" ], selectedNames selected.Left)

let private windowWith askPattern =
    focusedWindow
        { dependencies (mockFs ()) @"C:\start" with
            AskPattern = askPattern }

let private marked (list: ListView) =
    [ for i in 0 .. list.Source.Count - 1 do
          if list.Source.IsMarked i then
              string (list.Source.ToList()[i]) ]

[<Fact>]
let ``the select key asks for a pattern and marks the matches`` () =
    let asked = ResizeArray<string>()
    use window = windowWith (fun title -> asked.Add title; Some "*.fs")
    let list = leftList window

    press list PanelInput.selectPatternKey

    Assert.Equal<string list>([ Shell.SelectPrompt ], List.ofSeq asked)
    Assert.Collection(marked list, fun row -> Assert.StartsWith("setup.fs", row))

[<Fact>]
let ``the deselect key asks for a pattern and unmarks the matches`` () =
    let asked = ResizeArray<string>()
    use window = windowWith (fun title -> asked.Add title; Some "*.txt")
    let list = leftList window
    press list PanelInput.selectPatternKey
    let markedBefore = marked list

    press list PanelInput.deselectPatternKey

    Assert.Equal<string list>([ Shell.SelectPrompt; Shell.DeselectPrompt ], List.ofSeq asked)
    Assert.Equal(2, markedBefore.Length)
    Assert.Empty(marked list)

[<Fact>]
let ``cancelling the pattern prompt changes nothing`` () =
    use window = windowWith (fun _ -> None)
    let list = leftList window
    list.SelectedItem <- 1
    press list PanelInput.selectionKeys.Head

    press list PanelInput.selectPatternKey

    Assert.Collection(marked list, fun row -> Assert.StartsWith("docs", row))

[<Fact>]
let ``the panel shows a summary of what is selected`` () =
    use window = windowWith (fun _ -> Some "*.txt")
    let panelView = panel FilePanel.LeftId window
    let summaryBefore = (FilePanel.summary panelView).Text

    press (FilePanel.listView panelView) PanelInput.selectPatternKey

    Assert.Equal("", summaryBefore)
    Assert.Contains("2 selected", (FilePanel.summary panelView).Text)
    Assert.Contains("4 bytes", (FilePanel.summary panelView).Text)
