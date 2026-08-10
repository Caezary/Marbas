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
    create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") |> Expect.ok

let private names panel = panel.Entries |> List.map _.Name

let private selectedNames panel = selectedEntries panel |> List.map _.Name

let private windowWith (fs: MockFileSystem) askName reportError =
    focusedWindow
        { dependencies fs @"C:\start" with
            AskName = askName
            ReportError = reportError }

let private silent = fun (_: string) (_: string) -> ()

[<Fact>]
let ``making a directory adds it and lands the cursor on it`` () =
    let fs = mockFs ()
    let state = loaded fs

    let updated = makeDirectory fs Left "work" state |> Expect.ok

    Assert.True(fs.Directory.Exists @"C:\start\work")
    Assert.Contains("work", names updated.Left)
    Assert.Equal(Some "work", cursorEntry updated.Left |> Option.map _.Name)

[<Fact>]
let ``making a directory keeps the existing selection`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2 |> toggleSelectionAndAdvance Left

    let updated = makeDirectory fs Left "work" state |> Expect.ok

    Assert.Equal<string list>([ "notes.txt" ], selectedNames state.Left)
    Assert.Equal<string list>([ "notes.txt" ], selectedNames updated.Left)

[<Fact>]
let ``an existing name is refused and leaves the panel unchanged`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = makeDirectory fs Left "docs" state

    Assert.Equal(Error(TargetExists(fp @"C:\start\docs")), result)

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
    let fs = mockFs ()
    use window = windowWith fs (fun _ -> None) silent

    press window Key.F7

    Assert.Equal(3, (leftList window).Source.Count)

[<Fact>]
let ``a blank name creates nothing`` () =
    let fs = mockFs ()
    use window = windowWith fs (fun _ -> Some "   ") silent

    press window Key.F7

    Assert.Equal(3, (leftList window).Source.Count)

[<Fact>]
let ``a duplicate name is reported`` () =
    let reported = ResizeArray<string>()
    use window = windowWith (mockFs ()) (fun _ -> Some "docs") (fun _ -> reported.Add)

    press window Key.F7

    Assert.Contains("docs", Assert.Single reported)
