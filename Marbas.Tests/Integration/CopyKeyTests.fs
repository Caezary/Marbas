module Marbas.Tests.Integration.CopyKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem.Operations
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\start\docs\inner.txt", MockFileData "x")
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs.AddFile(@"C:\start\readme.txt", MockFileData "read me")
    fs.AddDirectory @"C:\backup"
    fs

let private windowWith (fs: MockFileSystem) askOverwrite reportError =
    let window =
        focusedWindow
            { Fakes.dependencies fs @"C:\start" with
                ChooseOption = (fun _ _ -> Some @"C:\backup")
                AskOverwrite = askOverwrite
                ReportError = reportError }

    press (leftList window) KeyMap.rightDriveKey
    window

let private silent = fun (_: string) (_: string) -> ()

let private noQuestion = fun (_: string) (_: string) -> Yes

[<Fact>]
let ``F5 copies the entry under the cursor into the other panel`` () =
    let fs = mockFs ()
    use window = windowWith fs noQuestion silent
    (leftList window).SelectedItem <- 2

    pressKey window Key.F5

    Assert.Equal("hi", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.Equal(2, (rightList window).Source.Count)

[<Fact>]
let ``F5 copies every marked entry`` () =
    let fs = mockFs ()
    use window = windowWith fs noQuestion silent
    let left = leftList window

    left.SelectedItem <- 1
    press left KeyMap.selectionKeys.Head
    left.SelectedItem <- 3
    press left KeyMap.selectionKeys.Head
    pressKey window Key.F5

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

    pressKey window Key.F5

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

    pressKey window Key.F5

    Assert.Equal("old", fs.File.ReadAllText @"C:\backup\notes.txt")

[<Fact>]
let ``no to all leaves every colliding target alone after one question`` () =
    let fs = mockFs ()
    fs.AddFile(@"C:\backup\notes.txt", MockFileData "old notes")
    fs.AddFile(@"C:\backup\readme.txt", MockFileData "old readme")
    let asked = ResizeArray<string * string>()
    use window = windowWith fs (fun title message -> asked.Add(title, message); NoToAll) silent
    let left = leftList window

    left.SelectedItem <- 2
    press left KeyMap.selectionKeys.Head
    left.SelectedItem <- 3
    press left KeyMap.selectionKeys.Head
    pressKey window Key.F5

    Assert.Single asked |> ignore
    Assert.Equal("old notes", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.Equal("old readme", fs.File.ReadAllText @"C:\backup\readme.txt")

[<Fact>]
let ``F5 on the parent entry copies nothing and asks nothing`` () =
    let fs = mockFs ()
    let asked = ResizeArray<string>()
    use window = windowWith fs (fun title _ -> asked.Add title; Yes) silent
    (leftList window).SelectedItem <- 0

    pressKey window Key.F5

    Assert.Empty asked
    Assert.Empty(fs.Directory.GetFileSystemEntries @"C:\backup")

[<Fact>]
let ``a failed copy is reported`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()
    use window = windowWith fs noQuestion (fun _ -> reported.Add)
    (leftList window).SelectedItem <- 2
    fs.File.Delete @"C:\start\notes.txt"

    pressKey window Key.F5

    Assert.Contains("notes.txt", Assert.Single reported)
