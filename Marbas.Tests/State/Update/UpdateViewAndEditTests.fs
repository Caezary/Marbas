module Marbas.Tests.State.Update.UpdateViewAndEditTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Locations
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Update.UpdateFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private opened path how =
    ChangeHistory(
        Remember(
            Opened
                { Where = Place.OnDisk(fp (at path))
                  How = how }
        )
    )

let private notesOpened how = opened "start/notes.txt" how

[<Fact>]
let ``view requested aims at the file under the cursor`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>([ notesOpened Viewed; ViewFile(fp (at "start/notes.txt")) ], result.Effects)

[<Fact>]
let ``view requested on a directory leaves the panel until the measurement is in`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1

    let result = update fs ViewRequested state

    Assert.Equal(withHelpAt ViewRequested state, result.State)

    Assert.Equal<Effect list>(
        [ MeasureTargets
              [ { Name = en "docs"
                  Location = OnDisk(fp (at "start/docs")) } ] ],
        result.Effects
    )

[<Fact>]
let ``view requested ignores what is marked and takes the cursor entry`` () =
    let fs = mockFs ()

    let state =
        loaded fs |> setCursor Left 3 |> toggleSelection Left |> setCursor Left 2

    let result = update fs ViewRequested state

    Assert.Equal<Effect list>([ notesOpened Viewed; ViewFile(fp (at "start/notes.txt")) ], result.Effects)

[<Fact>]
let ``edit requested aims at the file under the cursor`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs EditRequested state

    Assert.Equal<Effect list>([ notesOpened Edited; EditFile(fp (at "start/notes.txt"), None) ], result.Effects)

[<Fact>]
let ``edit requested names the editor the extension is associated with`` () =
    let fs = mockFs ()
    let chosen = Expect.some (ProgramPath.tryCreate "rider.exe")

    let programs =
        ProgramSettings.associate ForEditing (Expect.some (FileExtension.tryCreate "fs")) chosen ProgramSettings.none

    let state = loaded fs |> withPrograms programs |> setCursor Left 3

    let result = update fs EditRequested state

    Assert.Equal<Effect list>(
        [ opened "start/setup.fs" Edited
          EditFile(fp (at "start/setup.fs"), Some chosen) ],
        result.Effects
    )

[<Fact>]
let ``edit requested on a directory asks for its properties`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1
    let shown = EntryProperties.read fs (fp (at "start/docs")) |> Expect.ok

    let result = update fs EditRequested state

    Assert.Equal(withHelpAt EditRequested state, result.State)
    Assert.Equal<Effect list>([ ShowProperties shown ], result.Effects)

[<Fact>]
let ``edit requested on the parent entry raises no effect`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 0

    let result = update fs EditRequested state

    Assert.Equal(withHelpAt EditRequested state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``properties that come back are written and the panel re-read`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1

    let changed =
        EntryProperties.read fs (fp (at "start/docs"))
        |> Expect.ok
        |> EntryProperties.withFlag ReadOnlyFlag

    let result = update fs (PropertiesEntered changed) state

    Assert.Empty result.Effects

    Assert.True(
        EntryProperties.read fs (fp (at "start/docs"))
        |> Expect.ok
        |> EntryProperties.has ReadOnlyFlag
    )

[<Fact>]
let ``properties that cannot be written are reported`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 1
    let changed = EntryProperties.read fs (fp (at "start/docs")) |> Expect.ok
    fs.Directory.Delete(at "start/docs")

    let result = update fs (PropertiesEntered changed) state

    Assert.Equal<Effect list>([ Report(Problem.Operation(SourceNotFound(fp (at "start/docs")))) ], result.Effects)

[<Fact>]
let ``a new file is asked for by name`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs NewFileRequested state

    Assert.Equal<Effect list>([ PromptForFileName ], result.Effects)

[<Fact>]
let ``a named new file is created and handed to its editor`` () =
    let fs = mockFs ()
    let chosen = Expect.some (ProgramPath.tryCreate "code.exe")

    let programs =
        ProgramSettings.associate ForEditing (Expect.some (FileExtension.tryCreate "md")) chosen ProgramSettings.none

    let state = loaded fs |> withPrograms programs

    let result = update fs (FileNameEntered(en "draft.md")) state

    Assert.True(fs.File.Exists(at "start/draft.md"))

    Assert.Equal<Effect list>(
        [ opened "start/draft.md" Edited
          EditFile(fp (at "start/draft.md"), Some chosen) ],
        result.Effects
    )

    Assert.Equal(
        Some "draft.md",
        PanelState.cursorEntry result.State.Left
        |> Option.map (_.Name >> EntryName.value)
    )

[<Fact>]
let ``a new file a directory already answers to is reported`` () =
    let fs = mockFs ()
    let state = loaded fs

    let result = update fs (FileNameEntered(en "docs")) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ Report(Problem.Operation(TargetExists(fp (at "start/docs")))) ], result.Effects)

[<Fact>]
let ``a started editor leaves the panels to the directory watch`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2
    fs.AddFile(at "start/notes.txt.bak", MockFileData "hi")

    let result = update fs (EditorStarted(Ok())) state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``an editor that could not be started is reported`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2
    let failure = OperationFailed(fp (at "start/notes.txt"), "no editor")

    let result = update fs (EditorStarted(Error failure)) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ Report(Problem.Operation failure) ], result.Effects)

[<Fact>]
let ``an editor start the user turned down is silence, not a dialog`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result =
        update fs (EditorStarted(Error(StartDeclined(fp (at "start/notes.txt"))))) state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects
