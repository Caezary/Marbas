module Marbas.Tests.Integration.MkdirKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs

let private windowWith (fs: MockFileSystem) askName reportError =
    focusedWindow
        { Fakes.dependencies fs @"C:\start" with
            AskName = askName
            ReportError = reportError }

let private silent = fun (_: string) (_: string) -> ()

[<Fact>]
let ``F7 asks for a name and the new directory shows up under the cursor`` () =
    let asked = ResizeArray<string>()
    use window = windowWith (mockFs ()) (fun title -> asked.Add title; Some "work") silent

    press window Key.F7

    Assert.Equal<string list>([ Shell.MkdirPrompt ], List.ofSeq asked)
    let list = leftList window
    Assert.StartsWith("work", string (list.Source.ToList()[list.SelectedItem.GetValueOrDefault 0]))

[<Fact>]
let ``cancelling the name prompt creates nothing`` () =
    use window = windowWith (mockFs ()) (fun _ -> None) silent

    press window Key.F7

    Assert.Equal(3, (leftList window).Source.Count)

[<Fact>]
let ``a blank name creates nothing`` () =
    use window = windowWith (mockFs ()) (fun _ -> Some "   ") silent

    press window Key.F7

    Assert.Equal(3, (leftList window).Source.Count)

[<Fact>]
let ``a duplicate name is reported`` () =
    let reported = ResizeArray<string>()
    use window = windowWith (mockFs ()) (fun _ -> Some "docs") (fun _ -> reported.Add)

    press window Key.F7

    Assert.Contains("docs", Assert.Single reported)
