module Marbas.Tests.DeleteTests

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
    fs.AddDirectory @"C:\start\empty"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")
    fs

let private loaded (fs: IFileSystem) =
    match create (fp @"C:\start") |> loadPanel fs Left (fp @"C:\start") with
    | Ok state -> state
    | Error err -> failwith $"setup failed: %A{err}"

let private started () =
    let fs = mockFs ()
    fs, loaded fs

let private targetNames state = operationTargets state.Left |> List.map _.Name

let private deleted fs state =
    match deleteTargets fs Left state with
    | Ok result -> result
    | Error err -> failwith $"unexpected error %A{err}"

[<Fact>]
let ``with nothing marked the cursor entry is the only target`` () =
    let _, state = started ()

    Assert.Equal<string list>([ "notes.txt" ], state |> setCursor Left 3 |> targetNames)

[<Fact>]
let ``the parent entry is never a target`` () =
    let _, state = started ()

    Assert.Empty(state |> setCursor Left 0 |> targetNames)

[<Fact>]
let ``marked entries are the targets in listing order`` () =
    let _, state = started ()

    let marked =
        state
        |> setCursor Left 4
        |> toggleSelectionAndAdvance Left
        |> setCursor Left 1
        |> toggleSelectionAndAdvance Left

    Assert.Equal<string list>([ "docs"; "readme.txt" ], targetNames marked)

[<Fact>]
let ``deleting removes the cursor entry and refreshes the panel`` () =
    let fs, state = started ()

    let updated, _ = state |> setCursor Left 3 |> deleted fs

    Assert.False(fs.File.Exists @"C:\start\notes.txt")
    Assert.DoesNotContain("notes.txt", updated.Left.Entries |> List.map _.Name)

[<Fact>]
let ``deleting a directory takes its contents with it`` () =
    let fs, state = started ()

    let updated, _ = state |> setCursor Left 1 |> deleted fs

    Assert.False(fs.Directory.Exists @"C:\start\docs")
    Assert.DoesNotContain("docs", updated.Left.Entries |> List.map _.Name)

[<Fact>]
let ``every marked entry is deleted and reported`` () =
    let fs, state = started ()

    let marked =
        state
        |> setCursor Left 3
        |> toggleSelectionAndAdvance Left
        |> toggleSelectionAndAdvance Left

    let _, results = deleted fs marked

    Assert.Equal<string list>([ "notes.txt"; "readme.txt" ], results |> List.map _.Name)
    Assert.All(results, fun item -> Assert.Equal(Ok(), item.Outcome))
    Assert.False(fs.File.Exists @"C:\start\notes.txt")
    Assert.False(fs.File.Exists @"C:\start\readme.txt")

[<Fact>]
let ``a vanished entry is reported while the rest still go`` () =
    let fs, state = started ()

    let marked =
        state
        |> setCursor Left 3
        |> toggleSelectionAndAdvance Left
        |> toggleSelectionAndAdvance Left

    fs.File.Delete @"C:\start\notes.txt"
    let _, results = deleted fs marked

    Assert.Equal(Error(SourceNotFound(fp @"C:\start\notes.txt")), results[0].Outcome)
    Assert.Equal(Ok(), results[1].Outcome)
    Assert.False(fs.File.Exists @"C:\start\readme.txt")

[<Fact>]
let ``marks are dropped along with the entries they pointed at`` () =
    let fs, state = started ()

    let marked =
        state
        |> setCursor Left 3
        |> toggleSelectionAndAdvance Left
        |> setCursor Left 1
        |> toggleSelectionAndAdvance Left

    let updated, _ = deleted fs marked

    Assert.Empty(selectedEntries updated.Left)
    Assert.False(fs.Directory.Exists @"C:\start\docs")
    Assert.False(fs.File.Exists @"C:\start\notes.txt")

[<Fact>]
let ``a non-empty directory among the targets is recognised`` () =
    let fs, state = started ()

    Assert.True(hasNonEmptyDirectory fs (state |> setCursor Left 1).Left)

[<Fact>]
let ``an empty directory needs no extra warning`` () =
    let fs, state = started ()

    Assert.False(hasNonEmptyDirectory fs (state |> setCursor Left 2).Left)

let private windowWith fs confirm =
    let window =
        MainWindow.create
            { dependencies fs @"C:\start" with
                Confirm = confirm }

    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    window

[<Fact>]
let ``F8 asks for confirmation and deletes the entry under the cursor`` () =
    let fs = mockFs ()
    let mutable asked = []
    use window = windowWith fs (fun title message -> asked <- (title, message) :: asked; true)

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    list.SelectedItem <- 3
    press window Key.F8

    Assert.Equal(DeleteConfirmation.Title, fst (List.exactlyOne asked))
    Assert.Contains("notes.txt", snd (List.exactlyOne asked))
    Assert.False(fs.File.Exists @"C:\start\notes.txt")
    Assert.Equal(4, list.Source.Count)

[<Fact>]
let ``a refused confirmation deletes nothing`` () =
    let fs = mockFs ()
    use window = windowWith fs (fun _ _ -> false)

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    list.SelectedItem <- 3
    press window Key.F8

    Assert.True(fs.File.Exists @"C:\start\notes.txt")
    Assert.Equal(5, list.Source.Count)

[<Fact>]
let ``F8 on the parent entry asks nothing`` () =
    let fs = mockFs ()
    let mutable asked = 0
    use window = windowWith fs (fun _ _ -> asked <- asked + 1; true)

    press window Key.F8

    Assert.Equal(0, asked)

[<Fact>]
let ``a failed delete is reported`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()

    let window =
        MainWindow.create
            { dependencies fs @"C:\start" with
                Confirm = (fun _ _ -> true)
                ReportError = fun _ message -> reported.Add message }

    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    use window = window

    let list = FilePanel.listView (panel FilePanel.LeftId window)
    list.SelectedItem <- 3
    fs.File.Delete @"C:\start\notes.txt"
    press window Key.F8

    Assert.Single reported |> ignore
    Assert.Contains("notes.txt", reported[0])
