module Marbas.Tests.Integration.EditKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "hi")
    fs

let private windowWith (fs: MockFileSystem) runEditor reportError =
    focusedWindow
        { Fakes.dependencies fs @"C:\start" with
            RunEditor = runEditor
            ReportError = reportError }

let private silent = fun (_: string) (_: string) -> ()

[<Fact>]
let ``F4 hands the file under the cursor to the editor`` () =
    let edited = ResizeArray<FileSystemPath>()
    use window = windowWith (mockFs ()) (fun path -> edited.Add path; Ok()) silent
    (leftList window).SelectedItem <- 2

    press window Key.F4

    Assert.Equal(@"C:\start\notes.txt", FileSystemPath.value (Assert.Single edited))

[<Fact>]
let ``F4 on a directory starts no editor`` () =
    let edited = ResizeArray<FileSystemPath>()
    use window = windowWith (mockFs ()) (fun path -> edited.Add path; Ok()) silent
    (leftList window).SelectedItem <- 1

    press window Key.F4

    Assert.Empty edited

[<Fact>]
let ``what the editor left behind shows up when it returns`` () =
    let fs = mockFs ()
    let write () = fs.AddFile(@"C:\start\notes.txt.bak", MockFileData "hi")
    use window = windowWith fs (fun _ -> write (); Ok()) silent
    (leftList window).SelectedItem <- 2

    press window Key.F4

    Assert.Equal(4, (leftList window).Source.Count)

[<Fact>]
let ``an editor that could not be started is reported`` () =
    let reported = ResizeArray<string>()
    let failure = OperationFailed(FileSystemPath.create @"C:\start\notes.txt", "no editor")
    use window = windowWith (mockFs ()) (fun _ -> Error failure) (fun _ -> reported.Add)
    (leftList window).SelectedItem <- 2

    press window Key.F4

    Assert.Contains("notes.txt", Assert.Single reported)
