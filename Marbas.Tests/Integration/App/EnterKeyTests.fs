module Marbas.Tests.Integration.App.EnterKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/page.html", MockFileData "hi")
    fs

let private windowWith (fs: MockFileSystem) launch =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            Launch = launch
            ReportError = fun _ _ -> () }

let private Notes = 2

let private Page = 3

[<Fact>]
let ``Enter hands the document under the cursor to the system`` () =
    let started = ResizeArray<LaunchRequest>()
    use window = windowWith (mockFs ()) (fun request -> started.Add request; Ok())
    (leftList window).SelectedItem <- Notes

    pressKey window Key.Enter

    let request = Assert.Single started

    Assert.Equal(fp (at "start/notes.txt"), request.Target)
    Assert.Equal(None, request.Program)
    Assert.Equal(fp (at "start"), request.WorkingDirectory)

[<Fact>]
let ``Enter starts the program the extension is associated with`` () =
    let started = ResizeArray<LaunchRequest>()
    let fs = mockFs ()
    let chosen = Expect.some (ProgramPath.tryCreate "firefox.exe")

    let programs =
        ProgramSettings.associate ForOpening (Expect.some (FileExtension.tryCreate "html")) chosen ProgramSettings.none

    fs.AddFile(
        at "marbas.conf",
        MockFileData(
            String.concat
                "\n"
                (Settings.format
                    { Settings.forPath (fp (at "start")) with
                        Programs = programs })
        )
    )

    use window = windowWith fs (fun request -> started.Add request; Ok())
    (leftList window).SelectedItem <- Page

    pressKey window Key.Enter

    Assert.Equal(Some chosen, (Assert.Single started).Program)

[<Fact>]
let ``Enter on a directory walks into it and starts nothing`` () =
    let started = ResizeArray<LaunchRequest>()
    use window = windowWith (mockFs ()) (fun request -> started.Add request; Ok())
    (leftList window).SelectedItem <- 1

    pressKey window Key.Enter

    Assert.Empty started
    Assert.Equal(1, (leftList window).Source.Count)

[<Fact>]
let ``the walk-in key walks into a directory the way Enter used to`` () =
    let started = ResizeArray<LaunchRequest>()
    use window = windowWith (mockFs ()) (fun request -> started.Add request; Ok())
    (leftList window).SelectedItem <- 1

    pressKey window KeyMap.walkIntoKey

    Assert.Empty started
    Assert.Equal(1, (leftList window).Source.Count)

[<Fact>]
let ``a launch that fails is reported`` () =
    let reported = ResizeArray<string>()
    let fs = mockFs ()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Launch = fun _ -> Error(NothingAssociated(fp (at "start/notes.txt")))
                ReportError = fun _ -> reported.Add }

    (leftList window).SelectedItem <- Notes

    pressKey window Key.Enter

    Assert.Contains("notes.txt", Assert.Single reported)
