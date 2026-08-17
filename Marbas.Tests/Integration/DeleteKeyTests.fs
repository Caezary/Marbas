module Marbas.Tests.Integration.DeleteKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    fs.AddDirectory(at "start/empty")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    fs

let private windowWith (fs: MockFileSystem) confirm reportError =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            Confirm = confirm
            ReportError = reportError }

let private silent = fun (_: string) (_: string) -> ()

[<Fact>]
let ``F8 asks for confirmation and deletes the entry under the cursor`` () =
    let fs = mockFs ()
    let asked = ResizeArray<string * string>()
    use window = windowWith fs (fun title message -> asked.Add(title, message); true) silent
    let list = leftList window
    list.SelectedItem <- 3

    pressKey window Key.F8

    let title, message = Assert.Single asked
    Assert.Equal(DeleteConfirmation.Title, title)
    Assert.Contains("notes.txt", message)
    Assert.False(fs.File.Exists(at "start/notes.txt"))
    Assert.Equal(4, list.Source.Count)

[<Fact>]
let ``a refused confirmation deletes nothing`` () =
    let fs = mockFs ()
    use window = windowWith fs (fun _ _ -> false) silent
    let list = leftList window
    list.SelectedItem <- 3

    pressKey window Key.F8

    Assert.True(fs.File.Exists(at "start/notes.txt"))
    Assert.Equal(5, list.Source.Count)

[<Fact>]
let ``F8 on the parent entry asks nothing`` () =
    let asked = ResizeArray<string>()
    use window = windowWith (mockFs ()) (fun title _ -> asked.Add title; true) silent

    pressKey window Key.F8

    Assert.Empty asked

[<Fact>]
let ``a failed delete is reported`` () =
    let fs = mockFs ()
    let reported = ResizeArray<string>()
    use window = windowWith fs (fun _ _ -> true) (fun _ -> reported.Add)
    (leftList window).SelectedItem <- 3
    fs.File.Delete(at "start/notes.txt")

    pressKey window Key.F8

    Assert.Contains("notes.txt", Assert.Single reported)
