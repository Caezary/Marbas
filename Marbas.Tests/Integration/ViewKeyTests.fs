module Marbas.Tests.Integration.ViewKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Ui
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\notes.txt", MockFileData "first\r\nsecond")
    fs

let private windowWith (fs: MockFileSystem) showFile reportError =
    focusedWindow
        { Fakes.dependencies fs @"C:\start" with
            ShowFile = showFile
            ReportError = reportError }

let private silent = fun (_: string) (_: string) -> ()

[<Fact>]
let ``F3 shows the file under the cursor`` () =
    let shown = ResizeArray<string * string list>()
    use window = windowWith (mockFs ()) (fun title lines -> shown.Add(title, lines)) silent
    (leftList window).SelectedItem <- 2

    press window Key.F3

    let title, lines = Assert.Single shown
    Assert.Equal(@"C:\start\notes.txt", title)
    Assert.Equal<string list>([ "first"; "second" ], lines)

[<Fact>]
let ``F3 on a directory shows nothing`` () =
    let shown = ResizeArray<string>()
    use window = windowWith (mockFs ()) (fun title _ -> shown.Add title) silent
    (leftList window).SelectedItem <- 1

    press window Key.F3

    Assert.Empty shown

[<Fact>]
let ``F3 on the parent entry shows nothing`` () =
    let shown = ResizeArray<string>()
    use window = windowWith (mockFs ()) (fun title _ -> shown.Add title) silent
    (leftList window).SelectedItem <- 0

    press window Key.F3

    Assert.Empty shown

[<Fact>]
let ``a file that vanished is reported instead of shown`` () =
    let fs = mockFs ()
    let shown = ResizeArray<string>()
    let reported = ResizeArray<string>()
    use window = windowWith fs (fun title _ -> shown.Add title) (fun _ -> reported.Add)
    (leftList window).SelectedItem <- 2
    fs.File.Delete @"C:\start\notes.txt"

    press window Key.F3

    Assert.Empty shown
    Assert.Contains("notes.txt", Assert.Single reported)
