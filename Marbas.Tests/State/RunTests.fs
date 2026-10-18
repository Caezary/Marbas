module Marbas.Tests.State.RunTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Locations
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.ArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private tree () =
    let fs = packedAs Zip (at "start/backup.zip")
    fs.AddDirectory(at "start/docs")
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/page.html", MockFileData "hi")
    fs.AddFile(at "start/tool.exe", MockFileData "hi")
    fs

let private Parent = 0

let private Docs = 1

let private Backup = 2

let private Notes = 3

let private Page = 4

let private Tool = 5

let private opening extension program =
    ProgramSettings.associate
        ForOpening
        (Expect.some (FileExtension.tryCreate extension))
        (Expect.some (ProgramPath.tryCreate program))
        ProgramSettings.none

let private ran effect =
    match effect with
    | RunProgram request -> Some request
    | _ -> None

let private launched (result: UpdateResult) =
    Assert.Single result.Effects |> ran |> Expect.some

let private nothingRan (result: UpdateResult) =
    Assert.Empty(result.Effects |> List.choose ran)

[<Fact>]
let ``a document under the cursor is handed to the system`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Notes

    let request = update fs EntryOpened state |> launched

    Assert.Equal(fp (at "start/notes.txt"), request.Target)
    Assert.Equal(None, request.Program)
    Assert.Equal(fp (at "start"), request.WorkingDirectory)
    Assert.Equal(AsUser, request.Elevation)

[<Fact>]
let ``an executable under the cursor is handed to the system as well`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Tool

    let request = update fs EntryOpened state |> launched

    Assert.Equal(fp (at "start/tool.exe"), request.Target)
    Assert.Equal(None, request.Program)

[<Fact>]
let ``a file whose extension names a program runs that program`` () =
    let fs = tree ()

    let state =
        loaded fs |> withPrograms (opening "html" "firefox.exe") |> setCursor Left Page

    let request = update fs EntryOpened state |> launched

    Assert.Equal(fp (at "start/page.html"), request.Target)
    Assert.Equal(ProgramPath.tryCreate "firefox.exe", request.Program)

[<Fact>]
let ``the program an extension is edited with is not the one it runs`` () =
    let fs = tree ()

    let editing =
        ProgramSettings.associate
            ForEditing
            (Expect.some (FileExtension.tryCreate "html"))
            (Expect.some (ProgramPath.tryCreate "code.exe"))
            ProgramSettings.none

    let state = loaded fs |> withPrograms editing |> setCursor Left Page

    let request = update fs EntryOpened state |> launched

    Assert.Equal(None, request.Program)

[<Fact>]
let ``a directory under the cursor is walked into, not run`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Docs

    let result = update fs EntryOpened state

    Assert.Equal(OnDisk(fp (at "start/docs")), result.State.Left.Path)
    nothingRan result

[<Fact>]
let ``the parent entry is still the way up`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Parent

    let result = update fs EntryOpened state

    Assert.Equal(OnDisk(fp (at "")), result.State.Left.Path)
    nothingRan result

[<Fact>]
let ``an archive nothing is associated with opens as a location`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Backup

    let result = update fs EntryOpened state

    Assert.Equal(InArchive(fp (at "start/backup.zip"), ArchivePath.root), result.State.Left.Path)
    nothingRan result

[<Fact>]
let ``an archive whose extension names a program runs it instead of opening`` () =
    let fs = tree ()

    let state =
        loaded fs |> withPrograms (opening "zip" "7zfm.exe") |> setCursor Left Backup

    let result = update fs EntryOpened state

    Assert.Equal(OnDisk(fp (at "start")), result.State.Left.Path)
    Assert.Equal(fp (at "start/backup.zip"), (launched result).Target)

[<Fact>]
let ``walking in is what reaches an archive its extension would run`` () =
    let fs = tree ()

    let state =
        loaded fs |> withPrograms (opening "zip" "7zfm.exe") |> setCursor Left Backup

    let result = update fs WalkIntoRequested state

    Assert.Equal(InArchive(fp (at "start/backup.zip"), ArchivePath.root), result.State.Left.Path)
    nothingRan result

[<Fact>]
let ``the entry under the cursor runs, whatever is marked`` () =
    let fs = tree ()

    let marked =
        update fs (SelectionToggled Downward) (loaded fs |> setCursor Left Notes)

    let request = update fs EntryOpened marked.State |> launched

    Assert.Equal<string list>(
        [ "notes.txt" ],
        PanelState.selectedEntries marked.State.Left
        |> List.map (_.Name >> EntryName.value)
    )

    Assert.Equal(fp (at "start/page.html"), request.Target)

[<Fact>]
let ``a file inside an archive says why it will not run`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 2

    let result = update fs EntryOpened state

    Assert.Equal<Effect list>(
        [ Report(Problem.Operation(Operations.ArchiveEntryNotRunnable(fp (at "start/backup.zip/notes.txt")))) ],
        result.Effects
    )

[<Fact>]
let ``the way out of an archive is still the way out`` () =
    let fs = packed ()
    let state = standingInside fs Left

    let result = update fs EntryOpened state

    Assert.Equal(OnDisk(fp (at "start")), result.State.Left.Path)
    nothingRan result

[<Fact>]
let ``a directory inside an archive is still walked into`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 1

    let result = update fs EntryOpened state

    let docs = Expect.some (ArchivePath.tryCreate "docs")
    Assert.Equal(InArchive(archive, docs), result.State.Left.Path)
    Assert.Equal<Effect list>([ ChangeHistory(Remember(Visited(Place.InArchive(archive, docs)))) ], result.Effects)

[<Fact>]
let ``an executable under the cursor is started with administrator rights`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Tool

    let request = update fs ElevatedRunRequested state |> launched

    Assert.Equal(fp (at "start/tool.exe"), request.Target)
    Assert.Equal(None, request.Program)
    Assert.Equal(fp (at "start"), request.WorkingDirectory)
    Assert.Equal(AsAdministrator, request.Elevation)

[<Fact>]
let ``the rights raised are the executable's own, not an associated program's`` () =
    let fs = tree ()

    let state =
        loaded fs |> withPrograms (opening "exe" "sandbox.exe") |> setCursor Left Tool

    let request = update fs ElevatedRunRequested state |> launched

    Assert.Equal(fp (at "start/tool.exe"), request.Target)
    Assert.Equal(None, request.Program)

[<Fact>]
let ``a document has no rights of its own to raise`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Notes

    let result = update fs ElevatedRunRequested state

    Assert.Equal<Effect list>([ Report(Problem.Launch(NotAProgram(fp (at "start/notes.txt")))) ], result.Effects)

[<Fact>]
let ``a directory is no more a program than a document is`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Docs

    let result = update fs ElevatedRunRequested state

    Assert.Equal(OnDisk(fp (at "start")), result.State.Left.Path)
    Assert.Equal<Effect list>([ Report(Problem.Launch(NotAProgram(fp (at "start/docs")))) ], result.Effects)

[<Fact>]
let ``the parent entry is nothing to raise the rights of`` () =
    let fs = tree ()
    let state = loaded fs |> setCursor Left Parent

    let result = update fs ElevatedRunRequested state

    Assert.Equal(OnDisk(fp (at "start")), result.State.Left.Path)
    Assert.Empty result.Effects

[<Fact>]
let ``an entry inside an archive says why it will not run elevated either`` () =
    let fs = packed ()
    let state = standingInside fs Left |> setCursor Left 2

    let result = update fs ElevatedRunRequested state

    Assert.Equal<Effect list>(
        [ Report(Problem.Operation(Operations.ArchiveEntryNotRunnable(fp (at "start/backup.zip/notes.txt")))) ],
        result.Effects
    )
