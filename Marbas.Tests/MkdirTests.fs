module Marbas.Tests.MkdirTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State.AppState
open Marbas.Ui
open Marbas.Tests.TestHelpers

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs

let private loaded (fs: IFileSystem) =
    match create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") with
    | Ok state -> state
    | Error err -> failwith $"setup failed: %A{err}"

let private names panel = panel.Entries |> List.map _.Name

[<Fact>]
let ``making a directory adds it and lands the cursor on it`` () =
    let fs = mockFs ()

    match loaded fs |> makeDirectory fs Left "work" with
    | Ok state ->
        Assert.True(fs.Directory.Exists @"C:\start\work")
        Assert.Contains("work", names state.Left)
        Assert.Equal(Some "work", (cursorEntry state.Left |> Option.map _.Name))
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``making a directory keeps the existing selection`` () =
    let fs = mockFs ()

    let state =
        loaded fs |> setCursor Left 2 |> toggleSelectionAndAdvance Left

    Assert.Equal<string list>([ "notes.txt" ], selectedEntries state.Left |> List.map _.Name)

    match makeDirectory fs Left "work" state with
    | Ok updated -> Assert.Equal<string list>([ "notes.txt" ], selectedEntries updated.Left |> List.map _.Name)
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``an existing name is refused and leaves the panel unchanged`` () =
    let fs = mockFs ()

    Assert.Equal(Error(TargetExists(fp @"C:\start\docs")), loaded fs |> makeDirectory fs Left "docs")

let private windowWith askName =
    let window =
        MainWindow.create
            { dependencies (mockFs ()) @"C:\start" with
                AskName = askName }

    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    window

[<Fact>]
let ``F7 asks for a name and the new directory shows up under the cursor`` () =
    let mutable asked = []
    use window = windowWith (fun title -> asked <- title :: asked; Some "work")

    press window Key.F7

    Assert.Equal<string list>([ Shell.MkdirPrompt ], asked)
    let list = FilePanel.listView (panel FilePanel.LeftId window)
    let selected = string (list.Source.ToList()[list.SelectedItem.GetValueOrDefault 0])
    Assert.StartsWith("work", selected)

[<Fact>]
let ``cancelling the name prompt creates nothing`` () =
    let fs = mockFs ()

    let window =
        MainWindow.create
            { dependencies fs @"C:\start" with
                AskName = fun _ -> None }

    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    use window = window

    press window Key.F7

    Assert.False(fs.Directory.Exists @"C:\start\None")

[<Fact>]
let ``a blank name creates nothing`` () =
    let fs = mockFs ()

    let window =
        MainWindow.create
            { dependencies fs @"C:\start" with
                AskName = fun _ -> Some "   " }

    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    use window = window

    press window Key.F7

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    Assert.Equal(3, list.Source.Count)

[<Fact>]
let ``a duplicate name is reported`` () =
    let reported = ResizeArray<string>()

    let window =
        MainWindow.create
            { dependencies (mockFs ()) @"C:\start" with
                AskName = (fun _ -> Some "docs")
                ReportError = fun _ message -> reported.Add message }

    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    use window = window

    press window Key.F7

    Assert.Single reported |> ignore
    Assert.Contains("docs", reported[0])
