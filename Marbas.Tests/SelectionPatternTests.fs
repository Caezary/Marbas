module Marbas.Tests.SelectionPatternTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
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
    match create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") with
    | Ok state -> state
    | Error err -> failwith $"setup failed: %A{err}"

let private selectedNames panel =
    selectedEntries panel |> List.map (fun e -> e.Name)

[<Fact>]
let ``a pattern selects every matching entry in one step`` () =
    let state = loaded (mockFs ()) |> selectMatching Left "*.fs"

    Assert.Equal<string list>([ "setup.fs" ], selectedNames state.Left)

[<Fact>]
let ``pattern matching ignores case`` () =
    let state = loaded (mockFs ()) |> selectMatching Left "*.txt"

    Assert.Equal<string list>([ "notes.txt"; "readme.TXT" ], selectedNames state.Left)

[<Fact>]
let ``a pattern never selects the parent entry`` () =
    let state = loaded (mockFs ()) |> selectMatching Left "*"

    Assert.Equal<string list>([ "docs"; "notes.txt"; "readme.TXT"; "setup.fs" ], selectedNames state.Left)

[<Fact>]
let ``a pattern adds to what is already selected`` () =
    let state =
        loaded (mockFs ()) |> selectMatching Left "*.fs" |> selectMatching Left "*.txt"

    Assert.Equal<string list>([ "notes.txt"; "readme.TXT"; "setup.fs" ], selectedNames state.Left)

[<Fact>]
let ``a pattern deselects only the entries it matches`` () =
    let state =
        loaded (mockFs ())
        |> selectMatching Left "*"
        |> deselectMatching Left "*.txt"

    Assert.Equal<string list>([ "docs"; "setup.fs" ], selectedNames state.Left)

[<Fact>]
let ``a pattern that matches nothing leaves the selection alone`` () =
    let state =
        loaded (mockFs ())
        |> selectMatching Left "*.fs"
        |> selectMatching Left "*.md"
        |> deselectMatching Left "*.md"

    Assert.Equal<string list>([ "setup.fs" ], selectedNames state.Left)

let private windowWith askPattern =
    let window =
        MainWindow.create
            { dependencies (mockFs ()) @"C:\start" with
                AskPattern = askPattern }

    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    window

let private marked (list: Terminal.Gui.Views.ListView) =
    [ for i in 0 .. list.Source.Count - 1 do
          if list.Source.IsMarked i then
              string (list.Source.ToList()[i]) ]

[<Fact>]
let ``the select key asks for a pattern and marks the matches`` () =
    let mutable asked = []
    use window = windowWith (fun title -> asked <- title :: asked; Some "*.fs")

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    press list MainWindow.selectPatternKey

    Assert.Equal<string list>([ MainWindow.SelectPrompt ], asked)
    Assert.Collection(marked list, fun row -> Assert.StartsWith("setup.fs", row))

[<Fact>]
let ``the deselect key asks for a pattern and unmarks the matches`` () =
    let mutable asked = []

    use window =
        windowWith (fun title ->
            asked <- asked @ [ title ]
            Some "*.txt")

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    press list MainWindow.selectPatternKey
    Assert.Equal(2, (marked list).Length)

    press list MainWindow.deselectPatternKey

    Assert.Equal<string list>([ MainWindow.SelectPrompt; MainWindow.DeselectPrompt ], asked)
    Assert.Empty(marked list)

[<Fact>]
let ``cancelling the pattern prompt changes nothing`` () =
    use window = windowWith (fun _ -> None)

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    list.SelectedItem <- 1
    press list MainWindow.selectionKeys.Head
    press list MainWindow.selectPatternKey

    Assert.Collection(marked list, fun row -> Assert.StartsWith("docs", row))

[<Fact>]
let ``the panel shows a summary of what is selected`` () =
    use window = windowWith (fun _ -> Some "*.txt")
    let panelView = panel FilePanel.LeftId window

    Assert.Equal("", (FilePanel.summary panelView).Text)

    press (FilePanel.listView panelView) MainWindow.selectPatternKey

    Assert.Contains("2 selected", (FilePanel.summary panelView).Text)
    Assert.Contains("4 bytes", (FilePanel.summary panelView).Text)
