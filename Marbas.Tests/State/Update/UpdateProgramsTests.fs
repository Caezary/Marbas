module Marbas.Tests.State.Update.UpdateProgramsTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Programs
open Marbas.Domain
open Marbas.Messages
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Update.UpdateFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``a settings request hands over the programs in force`` () =
    let fs = mockFs ()

    let programs =
        { ProgramSettings.none with
            DefaultEditor = ProgramPath.tryCreate "code.exe" }

    let state = loaded fs |> withPrograms programs

    let result = update fs SettingsRequested state

    Assert.Equal<Effect list>([ ShowSettings(programs, None) ], result.Effects)

[<Fact>]
let ``a settings request offers the executable under the cursor`` () =
    let fs = mockFs ()
    fs.AddFile(at "start/rider64.exe", MockFileData "mz")
    let state = loaded fs |> setCursor Left 3

    let result = update fs SettingsRequested state

    Assert.Equal<Effect list>(
        [ ShowSettings(ProgramSettings.none, ProgramPath.tryCreate (at "start/rider64.exe")) ],
        result.Effects
    )

[<Fact>]
let ``a settings request offers nothing when the cursor is not on an executable`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs SettingsRequested state

    Assert.Equal<Effect list>([ ShowSettings(ProgramSettings.none, None) ], result.Effects)

[<Fact>]
let ``programs that come back take the place of the ones in force`` () =
    let fs = mockFs ()

    let programs =
        { ProgramSettings.none with
            DefaultEditor = ProgramPath.tryCreate "code.exe" }

    let result = update fs (ProgramsEntered programs) (loaded fs)

    Assert.Equal(programs, result.State.Programs)
    Assert.Empty result.Effects

let private launching target =
    { Target = fp (at target)
      Program = None
      WorkingDirectory = fp (at "start")
      Elevation = AsUser }

[<Fact>]
let ``a directory the system showed leaves the state alone and asks for nothing`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2

    let result = update fs (LaunchFinished(launching "start/docs", Ok())) state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects

[<Fact>]
let ``a program that could not be started is reported`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2
    let failure = NothingAssociated(fp (at "start/notes.txt"))

    let result =
        update fs (LaunchFinished(launching "start/notes.txt", Error failure)) state

    Assert.Equal(state, result.State)
    Assert.Equal<Effect list>([ Report(Problem.Launch failure) ], result.Effects)

[<Fact>]
let ``an elevation the user turned down is silence, not a dialog`` () =
    let fs = mockFs ()
    let state = loaded fs |> setCursor Left 2
    let declined = ElevationDeclined(fp (at "start/notes.txt"))

    let result =
        update fs (LaunchFinished(launching "start/notes.txt", Error declined)) state

    Assert.Equal(state, result.State)
    Assert.Empty result.Effects
