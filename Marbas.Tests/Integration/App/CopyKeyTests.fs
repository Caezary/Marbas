module Marbas.Tests.Integration.App.CopyKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Ui.Keys
open Marbas.Ui.Messages
open Marbas.Ui.App
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "read me")
    fs.AddDirectory(at "backup")
    fs

let private windowWith (fs: MockFileSystem) askOverwrite reportError =
    let window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Prompts.ChooseDrive = Fakes.choosingDrive (at "backup")
                Prompts.AskOverwrite = askOverwrite
                Prompts.ReportError = reportError }

    press (leftList window) KeyMap.rightRootKey
    window

let private silent = ignore<Ports.ErrorReport>

let private noQuestion = fun (_: Ports.OverwritePrompt) -> Yes

[<Fact>]
let ``F5 copies the entry under the cursor into the other panel`` () =
    let fs = mockFs ()
    use window = windowWith fs noQuestion silent
    (leftList window).SelectedItem <- 2

    pressKey window Key.F5

    Assert.Equal("hi", fs.File.ReadAllText(at "backup/notes.txt"))
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

    Assert.Equal("x", fs.File.ReadAllText(at "backup/docs/inner.txt"))
    Assert.Equal("read me", fs.File.ReadAllText(at "backup/readme.txt"))
    Assert.False(fs.File.Exists(at "backup/notes.txt"))

[<Fact>]
let ``F5 asks before replacing an existing entry`` () =
    let fs = mockFs ()
    fs.AddFile(at "backup/notes.txt", MockFileData "old")
    let asked = ResizeArray<FileSystemPath>()

    use window =
        windowWith
            fs
            (fun (request: Ports.OverwritePrompt) ->
                asked.Add request.Target
                Yes)
            silent

    (leftList window).SelectedItem <- 2

    pressKey window Key.F5

    let target = Assert.Single asked
    Assert.Equal(at "backup/notes.txt", FileSystemPath.value target)
    Assert.Equal("hi", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``a refused overwrite leaves the target alone`` () =
    let fs = mockFs ()
    fs.AddFile(at "backup/notes.txt", MockFileData "old")
    use window = windowWith fs (fun _ -> No) silent
    (leftList window).SelectedItem <- 2

    pressKey window Key.F5

    Assert.Equal("old", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``no to all leaves every colliding target alone after one question`` () =
    let fs = mockFs ()
    fs.AddFile(at "backup/notes.txt", MockFileData "old notes")
    fs.AddFile(at "backup/readme.txt", MockFileData "old readme")
    let asked = ResizeArray<FileSystemPath>()

    use window =
        windowWith
            fs
            (fun (request: Ports.OverwritePrompt) ->
                asked.Add request.Target
                NoToAll)
            silent

    let left = leftList window

    left.SelectedItem <- 2
    press left KeyMap.selectionKeys.Head
    left.SelectedItem <- 3
    press left KeyMap.selectionKeys.Head
    pressKey window Key.F5

    Assert.Single asked |> ignore
    Assert.Equal("old notes", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal("old readme", fs.File.ReadAllText(at "backup/readme.txt"))

[<Fact>]
let ``F5 on the parent entry copies nothing and asks nothing`` () =
    let fs = mockFs ()
    let asked = ResizeArray<string>()

    use window =
        windowWith
            fs
            (fun request ->
                asked.Add(FileSystemPath.value request.Target)
                Yes)
            silent

    (leftList window).SelectedItem <- 0

    pressKey window Key.F5

    Assert.Empty asked
    Assert.Empty(fs.Directory.GetFileSystemEntries(at "backup"))

[<Fact>]
let ``a failed copy is reported`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()
    use window = windowWith fs noQuestion (fun report -> reported.Add report.Message)
    (leftList window).SelectedItem <- 2
    fs.File.Delete(at "start/notes.txt")

    pressKey window Key.F5

    Assert.Contains("notes.txt", Assert.Single reported)
