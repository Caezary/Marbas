module Marbas.Tests.Integration.App.EditKeyTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Ui.Keys
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs

let private windowWith (fs: MockFileSystem) runEditor reportError =
    focusedWindow
        { Fakes.dependencies fs (at "start") with
            RunEditor = runEditor
            ReportError = reportError }

let private silent = fun (_: string) (_: string) -> ()

[<Fact>]
let ``F4 hands the file under the cursor to the editor`` () =
    let edited = ResizeArray<FileSystemPath>()
    use window = windowWith (mockFs ()) (fun _ path -> edited.Add path; Ok()) silent
    (leftList window).SelectedItem <- 2

    pressKey window Key.F4

    Assert.Equal(at "start/notes.txt", FileSystemPath.value (Assert.Single edited))

[<Fact>]
let ``F4 on a directory starts no editor`` () =
    let edited = ResizeArray<FileSystemPath>()
    use window = windowWith (mockFs ()) (fun _ path -> edited.Add path; Ok()) silent
    (leftList window).SelectedItem <- 1

    pressKey window Key.F4

    Assert.Empty edited

[<Fact>]
let ``F4 on a directory opens its properties`` () =
    let shown = ResizeArray<EntryProperties>()

    use window =
        focusedWindow
            { Fakes.dependencies (mockFs ()) (at "start") with
                EditProperties =
                    fun properties ->
                        shown.Add properties
                        None }

    (leftList window).SelectedItem <- 1

    pressKey window Key.F4

    Assert.Equal(at "start/docs", FileSystemPath.value (Assert.Single shown).Path)

[<Fact>]
let ``properties that come back from the popup reach the directory`` () =
    let fs = mockFs ()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                EditProperties = fun properties -> Some(EntryProperties.setFlag ReadOnlyFlag true properties) }

    (leftList window).SelectedItem <- 1

    pressKey window Key.F4

    let written = EntryProperties.read fs (FileSystemPath.create (at "start/docs")) |> Expect.ok
    Assert.True(EntryProperties.has ReadOnlyFlag written)

[<Fact>]
let ``F4 opens the editor its extension is associated with`` () =
    let started = ResizeArray<ProgramPath option>()
    let fs = mockFs ()
    let chosen = Expect.some (ProgramPath.tryCreate "notepad++.exe")

    let programs =
        ProgramSettings.associate ForEditing (Expect.some (FileExtension.tryCreate "txt")) chosen ProgramSettings.none

    fs.AddFile(
        at "marbas.conf",
        MockFileData(String.concat "\n" (Settings.format { Settings.forPath (FileSystemPath.create (at "start")) with Programs = programs }))
    )

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                RunEditor = fun editor _ -> started.Add editor; Ok() }

    (leftList window).SelectedItem <- 2

    pressKey window Key.F4

    Assert.Equal(Some chosen, Assert.Single started)

[<Fact>]
let ``F4 opens the editor a settings file written before the role named`` () =
    let started = ResizeArray<ProgramPath option>()
    let fs = mockFs ()

    fs.AddFile(
        at "marbas.conf",
        MockFileData(
            String.concat
                "\n"
                [ "left.path=" + at "start"
                  "right.path=" + at "start"
                  "editor.extension.txt=notepad++.exe" ]
        )
    )

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                RunEditor = fun editor _ -> started.Add editor; Ok() }

    (leftList window).SelectedItem <- 2

    pressKey window Key.F4

    Assert.Equal(ProgramPath.tryCreate "notepad++.exe", Assert.Single started)

[<Fact>]
let ``what the editor left behind shows up when it returns`` () =
    let fs = mockFs ()
    let write () = fs.AddFile(at "start/notes.txt.bak", MockFileData "hi")
    use window = windowWith fs (fun _ _ -> write (); Ok()) silent
    (leftList window).SelectedItem <- 2

    pressKey window Key.F4

    Assert.Equal(4, (leftList window).Source.Count)

[<Fact>]
let ``an editor that could not be started is reported`` () =
    let reported = ResizeArray<string>()
    let failure = OperationFailed(FileSystemPath.create (at "start/notes.txt"), "no editor")
    use window = windowWith (mockFs ()) (fun _ _ -> Error failure) (fun _ -> reported.Add)
    (leftList window).SelectedItem <- 2

    pressKey window Key.F4

    Assert.Contains("notes.txt", Assert.Single reported)

[<Fact>]
let ``Shift+F4 creates the named file in the panel directory and opens it`` () =
    let fs = mockFs ()
    let edited = ResizeArray<FileSystemPath>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                AskName = fun _ _ -> Some "draft.md"
                RunEditor = fun _ path -> edited.Add path; Ok() }

    pressKey window KeyMap.newFileKey

    Assert.True(fs.File.Exists(at "start/draft.md"))
    Assert.Equal(at "start/draft.md", FileSystemPath.value (Assert.Single edited))

[<Fact>]
let ``a cancelled Shift+F4 creates nothing`` () =
    let fs = mockFs ()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                AskName = fun _ _ -> None }

    pressKey window KeyMap.newFileKey

    Assert.Equal(3, (leftList window).Source.Count)

[<Fact>]
let ``F12 hands the programs in force to the settings popup and takes back what it returns`` () =
    let fs = mockFs ()
    let shown = ResizeArray<ProgramSettings>()
    let started = ResizeArray<ProgramPath option>()
    let chosen = Expect.some (ProgramPath.tryCreate "code.exe")

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                EditPrograms =
                    fun programs _ ->
                        shown.Add programs
                        Some { programs with DefaultEditor = Some chosen }
                RunEditor = fun editor _ -> started.Add editor; Ok() }

    pressKey window KeyMap.settingsKey
    (leftList window).SelectedItem <- 2
    pressKey window Key.F4

    Assert.Equal(ProgramSettings.none, Assert.Single shown)
    Assert.Equal(Some chosen, Assert.Single started)

[<Fact>]
let ``F12 offers the executable under the cursor to the settings popup`` () =
    let fs = mockFs ()
    fs.AddFile(at "start/rider64.exe", MockFileData "mz")
    let offered = ResizeArray<ProgramPath option>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                EditPrograms = fun _ candidate -> offered.Add candidate; None }

    (leftList window).SelectedItem <- 3

    pressKey window KeyMap.settingsKey

    Assert.Equal(ProgramPath.tryCreate (at "start/rider64.exe"), Assert.Single offered)

[<Fact>]
let ``F12 offers nothing when the cursor is not on an executable`` () =
    let fs = mockFs ()
    let offered = ResizeArray<ProgramPath option>()

    use window =
        focusedWindow
            { Fakes.dependencies fs (at "start") with
                EditPrograms = fun _ candidate -> offered.Add candidate; None }

    (leftList window).SelectedItem <- 2

    pressKey window KeyMap.settingsKey

    Assert.Equal(None, Assert.Single offered)
