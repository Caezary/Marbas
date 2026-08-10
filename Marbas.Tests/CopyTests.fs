module Marbas.Tests.CopyTests

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
    fs.AddFile(@"C:\start\docs\inner.txt", MockFileData "x")
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs.AddFile(@"C:\start\readme.txt", MockFileData "read me")
    fs.AddDirectory @"C:\backup"
    fs

let private load (fs: IFileSystem) side path state =
    loadPanel fs side path state |> Expect.ok

let private started () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start")
        |> load fs Left (fp @"C:\start")
        |> load fs Right (fp @"C:\backup")

    fs, state

let private markedNotes state =
    state |> setCursor Left 2 |> toggleSelectionAndAdvance Left

let private finished fs results state =
    finishTransfer fs results state |> Expect.ok |> fst

let private windowWith (fs: MockFileSystem) askOverwrite reportError =
    let window =
        focusedWindow
            { dependencies fs @"C:\start" with
                ChooseOption = (fun _ _ -> Some @"C:\backup")
                AskOverwrite = askOverwrite
                ReportError = reportError }

    press (leftList window) PanelInput.rightDriveKey
    window

let private silent = fun (_: string) (_: string) -> ()

let private noQuestion = fun (_: string) (_: string) -> Yes

[<Fact>]
let ``a finished copy lists the new entries in the other panel`` () =
    let fs, state = started ()
    let results = copyAll fs unattended [ fp @"C:\start\notes.txt" ] (fp @"C:\backup")

    let updated = finished fs results (markedNotes state)

    Assert.Contains("notes.txt", updated.Right.Entries |> List.map _.Name)

[<Fact>]
let ``marks are dropped from the entries that were copied`` () =
    let fs, state = started ()
    let results = copyAll fs unattended [ fp @"C:\start\notes.txt" ] (fp @"C:\backup")

    let updated = finished fs results (markedNotes state)

    Assert.Empty(selectedEntries updated.Left)

[<Fact>]
let ``a mark stays on an entry whose copy failed`` () =
    let fs, state = started ()
    let results =
        [ { Name = "notes.txt"
            Outcome = Error(AccessDenied(fp @"C:\backup\notes.txt")) } ]

    let updated = finished fs results (markedNotes state)

    Assert.Equal<string list>([ "notes.txt" ], selectedEntries updated.Left |> List.map _.Name)

[<Fact>]
let ``F5 copies the entry under the cursor into the other panel`` () =
    let fs = mockFs ()
    use window = windowWith fs noQuestion silent
    (leftList window).SelectedItem <- 2

    press window Key.F5

    Assert.Equal("hi", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.Equal(2, (rightList window).Source.Count)

[<Fact>]
let ``F5 copies every marked entry`` () =
    let fs = mockFs ()
    use window = windowWith fs noQuestion silent
    let left = leftList window

    left.SelectedItem <- 1
    press left PanelInput.selectionKeys.Head
    left.SelectedItem <- 3
    press left PanelInput.selectionKeys.Head
    press window Key.F5

    Assert.Equal("x", fs.File.ReadAllText @"C:\backup\docs\inner.txt")
    Assert.Equal("read me", fs.File.ReadAllText @"C:\backup\readme.txt")
    Assert.False(fs.File.Exists @"C:\backup\notes.txt")

[<Fact>]
let ``F5 asks before replacing an existing entry`` () =
    let fs = mockFs ()
    fs.AddFile(@"C:\backup\notes.txt", MockFileData "old")
    let asked = ResizeArray<string * string>()
    use window = windowWith fs (fun title message -> asked.Add(title, message); Yes) silent
    (leftList window).SelectedItem <- 2

    press window Key.F5

    let title, message = Assert.Single asked
    Assert.Equal(OverwriteQuestion.Title, title)
    Assert.Contains(@"C:\backup\notes.txt", message)
    Assert.Equal("hi", fs.File.ReadAllText @"C:\backup\notes.txt")

[<Fact>]
let ``a refused overwrite leaves the target alone`` () =
    let fs = mockFs ()
    fs.AddFile(@"C:\backup\notes.txt", MockFileData "old")
    use window = windowWith fs (fun _ _ -> No) silent
    (leftList window).SelectedItem <- 2

    press window Key.F5

    Assert.Equal("old", fs.File.ReadAllText @"C:\backup\notes.txt")

[<Fact>]
let ``F5 on the parent entry copies nothing and asks nothing`` () =
    let fs = mockFs ()
    let asked = ResizeArray<string>()
    use window = windowWith fs (fun title _ -> asked.Add title; Yes) silent
    (leftList window).SelectedItem <- 0

    press window Key.F5

    Assert.Empty asked
    Assert.Empty(fs.Directory.GetFileSystemEntries @"C:\backup")

[<Fact>]
let ``a failed copy is reported`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()
    use window = windowWith fs noQuestion (fun _ message -> reported.Add message)
    (leftList window).SelectedItem <- 2
    fs.File.Delete @"C:\start\notes.txt"

    press window Key.F5

    Assert.Contains("notes.txt", Assert.Single reported)
