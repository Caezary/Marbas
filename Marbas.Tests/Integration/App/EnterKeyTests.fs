module Marbas.Tests.Integration.App.EnterKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
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
    fs.AddFile(at "start/tool.exe", MockFileData "hi")
    fs

let private windowWith (fs: MockFileSystem) launch =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            Processes.Launch = launch
            Prompts.ReportError = ignore }

let private Notes = 2

let private Page = 3

let private Tool = 4

[<Fact>]
let ``Enter hands the document under the cursor to the system`` () =
    let started = ResizeArray<LaunchRequest>()

    use window =
        windowWith (mockFs ()) (fun request ->
            started.Add request
            Ok())

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

    use window =
        windowWith fs (fun request ->
            started.Add request
            Ok())

    (leftList window).SelectedItem <- Page

    pressKey window Key.Enter

    Assert.Equal(Some chosen, (Assert.Single started).Program)

[<Fact>]
let ``Enter on a directory walks into it and starts nothing`` () =
    let started = ResizeArray<LaunchRequest>()

    use window =
        windowWith (mockFs ()) (fun request ->
            started.Add request
            Ok())

    (leftList window).SelectedItem <- 1

    pressKey window Key.Enter

    Assert.Empty started
    Assert.Equal(1, (leftList window).Source.Count)

[<Fact>]
let ``the walk-in key walks into a directory the way Enter used to`` () =
    let started = ResizeArray<LaunchRequest>()

    use window =
        windowWith (mockFs ()) (fun request ->
            started.Add request
            Ok())

    (leftList window).SelectedItem <- 1

    pressKey window KeyMap.walkIntoKey

    Assert.Empty started
    Assert.Equal(1, (leftList window).Source.Count)

let private askedThrough key =
    let asked = ResizeArray<EntryName>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Prompts.AskHandler =
                    fun request ->
                        asked.Add request.Name
                        None }

    (leftList window).SelectedItem <- Page

    pressKey window key

    List.ofSeq asked

[<Fact>]
let ``Alt+R reaches the popup`` () =
    Assert.Equal<EntryName list>([ EntryName.create "page.html" ], askedThrough KeyMap.openWithKey)

[<Fact>]
let ``Shift+Enter reaches the popup, where a terminal delivers it`` () =
    Assert.Equal<EntryName list>([ EntryName.create "page.html" ], askedThrough KeyMap.classicOpenWithKey)

[<Fact>]
let ``the open-with key offers the programs the system knows and starts the one picked`` () =
    let started = ResizeArray<LaunchRequest>()
    let offered = ResizeArray<EntryName>()
    let chosen = Expect.some (ProgramPath.tryCreate "firefox.exe")

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Processes.Launch =
                    fun request ->
                        started.Add request
                        Ok()
                Processes.HandlersFor =
                    fun name ->
                        offered.Add name
                        []
                Prompts.AskHandler = fun _ -> Some { Program = chosen; Use = ThisTimeOnly } }

    (leftList window).SelectedItem <- Page

    pressKey window KeyMap.openWithKey

    Assert.Equal(EntryName.create "page.html", Assert.Single offered)
    Assert.Equal(Some chosen, (Assert.Single started).Program)

[<Fact>]
let ``the open-with key on a directory reaches the desktop without asking`` () =
    let started = ResizeArray<LaunchRequest>()
    let asked = ResizeArray<EntryName>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Processes.Launch =
                    fun request ->
                        started.Add request
                        Ok()
                Prompts.AskHandler =
                    fun request ->
                        asked.Add request.Name
                        None }

    (leftList window).SelectedItem <- 1

    pressKey window KeyMap.openWithKey

    Assert.Empty asked
    Assert.Equal(fp (at "start/docs"), (Assert.Single started).Target)

[<Fact>]
let ``a launch that fails is reported`` () =
    let reported = ResizeArray<string>()
    let fs = mockFs ()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                Processes.Launch = fun _ -> Error(NothingAssociated(fp (at "start/notes.txt")))
                Prompts.ReportError = fun report -> reported.Add report.Message }

    (leftList window).SelectedItem <- Notes

    pressKey window Key.Enter

    Assert.Contains("notes.txt", Assert.Single reported)

[<Fact>]
let ``the elevated key hands an executable to the system with its rights raised`` () =
    let started = ResizeArray<LaunchRequest>()

    use window =
        windowWith (mockFs ()) (fun request ->
            started.Add request
            Ok())

    (leftList window).SelectedItem <- Tool

    pressKey window KeyMap.elevatedRunKey

    let request = Assert.Single started

    Assert.Equal(fp (at "start/tool.exe"), request.Target)
    Assert.Equal(AsAdministrator, request.Elevation)

[<Fact>]
let ``the elevated key on a document starts nothing and says why`` () =
    let started = ResizeArray<LaunchRequest>()
    let reported = ResizeArray<string>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Processes.Launch =
                    fun request ->
                        started.Add request
                        Ok()
                Prompts.ReportError = fun report -> reported.Add report.Message }

    (leftList window).SelectedItem <- Notes

    pressKey window KeyMap.elevatedRunKey

    Assert.Empty started
    Assert.Contains("notes.txt", Assert.Single reported)

[<Fact>]
let ``an elevation the user turns down leaves no trace on the screen`` () =
    let reported = ResizeArray<string>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                Processes.Launch = fun request -> Error(ElevationDeclined request.Target)
                Prompts.ReportError = fun report -> reported.Add report.Message }

    (leftList window).SelectedItem <- Tool

    pressKey window KeyMap.elevatedRunKey

    Assert.Empty reported

[<Fact>]
let ``Ctrl+PgDn walks in as well, where a terminal delivers it`` () =
    let started = ResizeArray<LaunchRequest>()

    use window =
        windowWith (mockFs ()) (fun request ->
            started.Add request
            Ok())

    (leftList window).SelectedItem <- 1

    pressKey window KeyMap.classicWalkIntoKey

    Assert.Empty started
    Assert.Equal(1, (leftList window).Source.Count)

[<Fact>]
let ``Ctrl+Alt+Enter raises the rights as well, where a terminal delivers it`` () =
    let started = ResizeArray<LaunchRequest>()

    use window =
        windowWith (mockFs ()) (fun request ->
            started.Add request
            Ok())

    (leftList window).SelectedItem <- Tool

    pressKey window KeyMap.classicElevatedRunKey

    Assert.Equal(AsAdministrator, (Assert.Single started).Elevation)
