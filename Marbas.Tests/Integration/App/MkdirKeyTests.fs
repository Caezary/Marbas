module Marbas.Tests.Integration.App.MkdirKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Terminal.Gui.Input
open Marbas.Ui.Keys
open Marbas.Ui.App
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs

let private windowWith (fs: MockFileSystem) askName reportError =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            Prompts.AskName = askName
            Prompts.ReportError = reportError }

let private silent = ignore<Ports.ErrorReport>

let private typed raw =
    EnteredText.tryCreate raw |> Expect.some

[<Fact>]
let ``F7 asks for a name and the new directory shows up under the cursor`` () =
    let asked = ResizeArray<string>()

    use window =
        windowWith
            (mockFs ())
            (fun request ->
                asked.Add request.Title
                Some "work")
            silent

    pressKey window Key.F7

    Assert.Equal<string list>([ Shell.MkdirPrompt ], List.ofSeq asked)
    let list = leftList window
    Assert.StartsWith("work", string (list.Source.ToList()[list.SelectedItem.GetValueOrDefault 0]))

[<Fact>]
let ``cancelling the name prompt creates nothing`` () =
    use window = windowWith (mockFs ()) (fun _ -> None) silent

    pressKey window Key.F7

    Assert.Equal(3, (leftList window).Source.Count)

[<Fact>]
let ``a duplicate name is reported`` () =
    let reported = ResizeArray<string>()

    use window =
        windowWith (mockFs ()) (fun _ -> Some "docs") (fun report -> reported.Add report.Message)

    pressKey window Key.F7

    Assert.Contains("docs", Assert.Single reported)

[<Fact>]
let ``F7 opens with nothing to suggest until a directory has been made`` () =
    let offered = ResizeArray<EnteredText option>()

    use window =
        windowWith
            (mockFs ())
            (fun request ->
                offered.Add request.Suggestion
                None)
            silent

    pressKey window Key.F7

    Assert.Equal<EnteredText option list>([ None ], List.ofSeq offered)

[<Fact>]
let ``F7 offers the name the last directory was given`` () =
    let offered = ResizeArray<EnteredText option>()
    let answer = ref "work"

    use window =
        windowWith
            (mockFs ())
            (fun request ->
                offered.Add request.Suggestion
                Some answer.Value)
            silent

    pressKey window Key.F7
    answer.Value <- "reports"
    pressKey window Key.F7
    pressKey window Key.F7

    Assert.Equal<EnteredText option list>([ None; Some(typed "work"); Some(typed "reports") ], List.ofSeq offered)

[<Fact>]
let ``the name the last directory was given outlives the session`` () =
    let fs = mockFs ()
    let offered = ResizeArray<EnteredText option>()

    use first = windowWith fs (fun _ -> Some "work") silent
    pressKey first Key.F7
    pressKey first KeyMap.quitKey

    use second =
        windowWith
            fs
            (fun request ->
                offered.Add request.Suggestion
                None)
            silent

    pressKey second Key.F7

    Assert.Equal<EnteredText option list>([ Some(typed "work") ], List.ofSeq offered)
