module Marbas.Tests.Integration.ViewKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "first\r\nsecond")
    fs

let private windowWith (fs: MockFileSystem) showFile reportError =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            ShowFile = showFile
            ReportError = reportError }

let private silent = fun (_: string) (_: string) -> ()

let private recording (shown: ResizeArray<string>) =
    fun (source: FileWindow.Source) -> shown.Add(FileSystemPath.value source.Path)

[<Fact>]
let ``F3 shows the file under the cursor`` () =
    let shown = ResizeArray<string>()
    use window = windowWith (mockFs ()) (recording shown) silent
    (leftList window).SelectedItem <- 2

    pressKey window Key.F3

    Assert.Equal(at "start/notes.txt", Assert.Single shown)

[<Fact>]
let ``the file is handed over ready to read`` () =
    let read = ResizeArray<string>()

    let readFirstLine (source: FileWindow.Source) =
        LineIndex.Index(source).Text 0 |> Option.iter read.Add

    use window = windowWith (mockFs ()) readFirstLine silent
    (leftList window).SelectedItem <- 2

    pressKey window Key.F3

    Assert.Equal("first", Assert.Single read)

[<Fact>]
let ``F3 on a directory shows nothing`` () =
    let shown = ResizeArray<string>()
    use window = windowWith (mockFs ()) (recording shown) silent
    (leftList window).SelectedItem <- 1

    pressKey window Key.F3

    Assert.Empty shown

[<Fact>]
let ``F3 on the parent entry shows nothing`` () =
    let shown = ResizeArray<string>()
    use window = windowWith (mockFs ()) (recording shown) silent
    (leftList window).SelectedItem <- 0

    pressKey window Key.F3

    Assert.Empty shown

[<Fact>]
let ``a file that vanished is reported instead of shown`` () =
    let fs = mockFs ()
    let shown = ResizeArray<string>()
    let reported = ResizeArray<string>()
    use window = windowWith fs (recording shown) (fun _ -> reported.Add)
    (leftList window).SelectedItem <- 2
    fs.File.Delete(at "start/notes.txt")

    pressKey window Key.F3

    Assert.Empty shown
    Assert.Contains("notes.txt", Assert.Single reported)
