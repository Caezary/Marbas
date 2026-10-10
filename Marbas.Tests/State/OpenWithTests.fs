module Marbas.Tests.State.OpenWithTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.Messages
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private program raw = Expect.some (ProgramPath.tryCreate raw)

let private tree () =
    let fs = packedAs Zip (at "start/backup.zip")
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/page.html", MockFileData "hi")
    fs

let private Parent = 0

let private Docs = 1

let private Backup = 2

let private Page = 3

let private ran effect =
    match effect with
    | RunProgram request -> Some request
    | _ -> None

let private launched (result: UpdateResult) =
    Assert.Single result.Effects |> ran |> Expect.some

let private choosing raw handlerUse =
    { Program = program raw
      Use = handlerUse }

[<Fact>]
let ``a file under the cursor asks which program should open it`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Page

    let result = update fs OpenWithRequested state

    Assert.Equal<Effect list>([ ShowHandlers(fp (at "start/page.html"), None) ], result.Effects)

[<Fact>]
let ``the program the extension already names is the one offered`` () =
    let fs = tree ()
    let chosen = program "firefox.exe"

    let programs =
        ProgramSettings.associate ForOpening (Expect.some (FileExtension.tryCreate "html")) chosen ProgramSettings.none

    let state = loaded fs |> withPrograms programs |> setCursor Left Page

    let result = update fs OpenWithRequested state

    Assert.Equal<Effect list>([ ShowHandlers(fp (at "start/page.html"), Some chosen) ], result.Effects)

[<Fact>]
let ``a directory under the cursor reaches the desktop instead of a popup`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Docs

    let request = update fs OpenWithRequested state |> launched

    Assert.Equal(fp (at "start/docs"), request.Target)
    Assert.Equal(None, request.Program)
    Assert.Equal(fp (at "start"), request.WorkingDirectory)

[<Fact>]
let ``the parent entry reaches the directory the panel stands in`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Parent

    let request = update fs OpenWithRequested state |> launched

    Assert.Equal(fp (at "start"), request.Target)

[<Fact>]
let ``an archive under the cursor is a file like any other`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Backup

    let result = update fs OpenWithRequested state

    Assert.Equal<Effect list>([ ShowHandlers(fp (at "start/backup.zip"), None) ], result.Effects)

[<Fact>]
let ``a panel standing inside an archive refuses`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 2

    let result = update fs OpenWithRequested state

    Assert.Equal<Effect list>(
        [ Report(Problem.Operation(Operations.ArchiveEntryNotRunnable(fp (at "start/backup.zip/notes.txt")))) ],
        result.Effects
    )

[<Fact>]
let ``a panel inside an archive refuses on the way out too`` () =
    let fs = packed ()
    let state = standingInside fs Left

    let result = update fs OpenWithRequested state

    Assert.Equal<Effect list>(
        [ Report(Problem.Operation(Operations.ArchiveEntryNotRunnable(fp (at "start/backup.zip")))) ],
        result.Effects
    )

[<Fact>]
let ``the program that was picked is the one started`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Page

    let result =
        update fs (HandlerChosen(fp (at "start/page.html"), choosing "firefox.exe" ThisTimeOnly)) state

    let request = launched result

    Assert.Equal(fp (at "start/page.html"), request.Target)
    Assert.Equal(Some(program "firefox.exe"), request.Program)
    Assert.Equal(fp (at "start"), request.WorkingDirectory)

[<Fact>]
let ``a choice that is not remembered leaves the table alone`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Page

    let result =
        update fs (HandlerChosen(fp (at "start/page.html"), choosing "firefox.exe" ThisTimeOnly)) state

    Assert.Equal(ProgramSettings.none, result.State.Programs)

[<Fact>]
let ``a remembered choice becomes the row the next Enter reads`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Page

    let result =
        update fs (HandlerChosen(fp (at "start/page.html"), choosing "firefox.exe" AlwaysThisWay)) state

    Assert.Equal(
        Some(program "firefox.exe"),
        ProgramSettings.programFor ForOpening (EntryName.create "other.html") result.State.Programs
    )

[<Fact>]
let ``a remembered choice goes into the settings the session is written from`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Page

    let result =
        update fs (HandlerChosen(fp (at "start/page.html"), choosing "firefox.exe" AlwaysThisWay)) state

    Assert.Contains("program.opening.html=firefox.exe", Settings.format (settings fs result.State))

[<Fact>]
let ``a name with no extension is started but remembered against nothing`` () =
    let fs = tree ()
    fs.AddFile(at "start/README", MockFileData "hi")
    let state = loaded fs |> setCursor Left Page

    let result =
        update fs (HandlerChosen(fp (at "start/README"), choosing "vi" AlwaysThisWay)) state

    Assert.Equal(ProgramSettings.none, result.State.Programs)
    Assert.Equal(fp (at "start/README"), (launched result).Target)
