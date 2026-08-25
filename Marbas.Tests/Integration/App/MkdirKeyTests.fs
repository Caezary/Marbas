module Marbas.Tests.Integration.App.MkdirKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
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
            AskName = askName
            ReportError = reportError }

let private silent = fun (_: string) (_: string) -> ()

[<Fact>]
let ``F7 asks for a name and the new directory shows up under the cursor`` () =
    let asked = ResizeArray<string>()
    use window = windowWith (mockFs ()) (fun title _ -> asked.Add title; Some "work") silent

    pressKey window Key.F7

    Assert.Equal<string list>([ Shell.MkdirPrompt ], List.ofSeq asked)
    let list = leftList window
    Assert.StartsWith("work", string (list.Source.ToList()[list.SelectedItem.GetValueOrDefault 0]))

[<Fact>]
let ``cancelling the name prompt creates nothing`` () =
    use window = windowWith (mockFs ()) (fun _ _ -> None) silent

    pressKey window Key.F7

    Assert.Equal(3, (leftList window).Source.Count)

[<Fact>]
let ``a blank name creates nothing`` () =
    use window = windowWith (mockFs ()) (fun _ _ -> Some "   ") silent

    pressKey window Key.F7

    Assert.Equal(3, (leftList window).Source.Count)

[<Fact>]
let ``a duplicate name is reported`` () =
    let reported = ResizeArray<string>()
    use window = windowWith (mockFs ()) (fun _ _ -> Some "docs") (fun _ -> reported.Add)

    pressKey window Key.F7

    Assert.Contains("docs", Assert.Single reported)

[<Fact>]
let ``F7 opens with nothing to suggest until a directory has been made`` () =
    let offered = ResizeArray<string>()
    use window = windowWith (mockFs ()) (fun _ suggestion -> offered.Add suggestion; None) silent

    pressKey window Key.F7

    Assert.Equal<string list>([ "" ], List.ofSeq offered)

[<Fact>]
let ``F7 offers the name the last directory was given`` () =
    let offered = ResizeArray<string>()
    let answer = ref "work"
    use window = windowWith (mockFs ()) (fun _ suggestion -> offered.Add suggestion; Some answer.Value) silent

    pressKey window Key.F7
    answer.Value <- "reports"
    pressKey window Key.F7
    pressKey window Key.F7

    Assert.Equal<string list>([ ""; "work"; "reports" ], List.ofSeq offered)

[<Fact>]
let ``the name the last directory was given outlives the session`` () =
    let fs = mockFs ()
    let offered = ResizeArray<string>()

    use first = windowWith fs (fun _ _ -> Some "work") silent
    pressKey first Key.F7
    pressKey first KeyMap.quitKey

    use second = windowWith fs (fun _ suggestion -> offered.Add suggestion; None) silent
    pressKey second Key.F7

    Assert.Equal<string list>([ "work" ], List.ofSeq offered)
