module Marbas.Tests.MoveTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem.Operations
open Marbas.Ui
open Marbas.Tests.TestHelpers

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\start\docs\inner.txt", MockFileData "x")
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs.AddFile(@"C:\start\readme.txt", MockFileData "read me")
    fs.AddDirectory @"C:\backup"
    fs

let private windowWith (fs: MockFileSystem) askTarget askOverwrite reportError =
    let window =
        focusedWindow
            { dependencies fs @"C:\start" with
                ChooseOption = (fun _ _ -> Some @"C:\backup")
                AskTarget = askTarget
                AskOverwrite = askOverwrite
                ReportError = reportError }

    press (leftList window) PanelInput.rightDriveKey
    window

let private silent = fun (_: string) (_: string) -> ()

let private accepting = fun _ -> Some

let private refusing = fun _ _ -> None

let private noQuestion = fun (_: string) (_: string) -> Yes

[<Fact>]
let ``F6 offers the other panel's path`` () =
    let fs = mockFs ()
    let offered = ResizeArray<string * string>()
    use window = windowWith fs (fun title suggestion -> offered.Add(title, suggestion); None) noQuestion silent
    (leftList window).SelectedItem <- 2

    press window Key.F6

    let title, suggestion = Assert.Single offered
    Assert.Equal(Shell.MovePrompt, title)
    Assert.Equal(@"C:\backup", suggestion)

[<Fact>]
let ``F6 moves the entry under the cursor into the other panel`` () =
    let fs = mockFs ()
    use window = windowWith fs accepting noQuestion silent
    let left = leftList window
    left.SelectedItem <- 2

    press window Key.F6

    Assert.Equal("hi", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.False(fs.File.Exists @"C:\start\notes.txt")
    Assert.Equal(3, left.Source.Count)

[<Fact>]
let ``F6 moves every marked entry`` () =
    let fs = mockFs ()
    use window = windowWith fs accepting noQuestion silent
    let left = leftList window

    left.SelectedItem <- 1
    press left PanelInput.selectionKeys.Head
    left.SelectedItem <- 3
    press left PanelInput.selectionKeys.Head
    press window Key.F6

    Assert.Equal("x", fs.File.ReadAllText @"C:\backup\docs\inner.txt")
    Assert.Equal("read me", fs.File.ReadAllText @"C:\backup\readme.txt")
    Assert.False(fs.Directory.Exists @"C:\start\docs")
    Assert.True(fs.File.Exists @"C:\start\notes.txt")

[<Fact>]
let ``a bare name renames the entry under the cursor`` () =
    let fs = mockFs ()
    use window = windowWith fs (fun _ _ -> Some "aaa.txt") noQuestion silent
    (leftList window).SelectedItem <- 2

    press window Key.F6

    Assert.Equal("hi", fs.File.ReadAllText @"C:\start\aaa.txt")
    Assert.False(fs.File.Exists @"C:\start\notes.txt")
    Assert.Empty(fs.Directory.GetFileSystemEntries @"C:\backup")

[<Fact>]
let ``the renamed entry keeps the cursor`` () =
    let fs = mockFs ()
    use window = windowWith fs (fun _ _ -> Some "aaa.txt") noQuestion silent
    let left = leftList window
    left.SelectedItem <- 2

    press window Key.F6

    let row = string (left.Source.ToList()[left.SelectedItem.GetValueOrDefault 0])
    Assert.StartsWith("aaa.txt", row)

[<Fact>]
let ``cancelling the prompt moves nothing`` () =
    let fs = mockFs ()
    use window = windowWith fs refusing noQuestion silent
    (leftList window).SelectedItem <- 2

    press window Key.F6

    Assert.True(fs.File.Exists @"C:\start\notes.txt")
    Assert.Empty(fs.Directory.GetFileSystemEntries @"C:\backup")

[<Fact>]
let ``F6 on the parent entry asks nothing`` () =
    let fs = mockFs ()
    let asked = ResizeArray<string>()
    use window = windowWith fs (fun title suggestion -> asked.Add title; Some suggestion) noQuestion silent
    (leftList window).SelectedItem <- 0

    press window Key.F6

    Assert.Empty asked
    Assert.Empty(fs.Directory.GetFileSystemEntries @"C:\backup")

[<Fact>]
let ``a collision is asked about before the target is replaced`` () =
    let fs = mockFs ()
    fs.AddFile(@"C:\backup\notes.txt", MockFileData "old")
    let asked = ResizeArray<string>()
    use window = windowWith fs accepting (fun _ message -> asked.Add message; No) silent
    (leftList window).SelectedItem <- 2

    press window Key.F6

    Assert.Contains(@"C:\backup\notes.txt", Assert.Single asked)
    Assert.Equal("old", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.True(fs.File.Exists @"C:\start\notes.txt")

[<Fact>]
let ``a failed move is reported`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()
    use window = windowWith fs accepting noQuestion (fun _ message -> reported.Add message)
    (leftList window).SelectedItem <- 2
    fs.File.Delete @"C:\start\notes.txt"

    press window Key.F6

    Assert.Contains("notes.txt", Assert.Single reported)

[<Fact>]
let ``a rename onto an existing name is reported`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()
    use window = windowWith fs (fun _ _ -> Some "readme.txt") noQuestion (fun _ message -> reported.Add message)
    (leftList window).SelectedItem <- 2

    press window Key.F6

    Assert.Contains("readme.txt", Assert.Single reported)
    Assert.Equal("read me", fs.File.ReadAllText @"C:\start\readme.txt")
