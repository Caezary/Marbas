module Marbas.Tests.Ui.App.ShellTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui.Messages
open Marbas.Ui.App
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private noDeps () = Fakes.dependencies (MockFileSystem()) (at "start")

let private deleteRequest names scope =
    { Targets = names
      Scope = scope
      Mode = ToTrash }

let private cursor side index = { Side = side; Index = index }

let private capture () =
    let sent = ResizeArray<Message>()
    sent, fun (msg: Message) -> sent.Add msg

[<Fact>]
let ``a supplied name is dispatched as NameEntered`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with AskName = fun _ _ -> Some "work" }

    Shell.interpret deps dispatch (PromptForName None)

    Assert.Equal<Message list>([ NameEntered(en "work") ], List.ofSeq sent)

[<Fact>]
let ``a name is trimmed before it becomes a message`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with AskName = fun _ _ -> Some "  work  " }

    Shell.interpret deps dispatch (PromptForName None)

    Assert.Equal<Message list>([ NameEntered(en "work") ], List.ofSeq sent)

[<Fact>]
let ``a blank name never becomes a message`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with AskName = fun _ _ -> Some "   " }

    Shell.interpret deps dispatch (PromptForName None)

    Assert.Empty sent

[<Fact>]
let ``a cancelled name prompt dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (PromptForName None)

    Assert.Empty sent

[<Fact>]
let ``the name prompt uses the mkdir label`` () =
    let _, dispatch = capture ()
    let mutable asked = ""
    let deps = { noDeps () with AskName = fun title _ -> asked <- title; None }

    Shell.interpret deps dispatch (PromptForName None)

    Assert.Equal(Shell.MkdirPrompt, asked)

[<Fact>]
let ``a selecting pattern uses the select label and carries the answer back`` () =
    let sent, dispatch = capture ()
    let mutable asked = ""
    let deps = { noDeps () with AskPattern = fun title -> asked <- title; Some "*.fs" }

    Shell.interpret deps dispatch (PromptForPattern Select)

    Assert.Equal(Shell.SelectPrompt, asked)

    Assert.Equal<Message list>(
        [ PatternEntered(Select, NamePattern.tryCreate "*.fs" |> Expect.some) ],
        List.ofSeq sent
    )

[<Fact>]
let ``a deselecting pattern uses the deselect label`` () =
    let _, dispatch = capture ()
    let mutable asked = ""
    let deps = { noDeps () with AskPattern = fun title -> asked <- title; None }

    Shell.interpret deps dispatch (PromptForPattern Deselect)

    Assert.Equal(Shell.DeselectPrompt, asked)

[<Fact>]
let ``an empty pattern never becomes a message`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with AskPattern = fun _ -> Some "" }

    Shell.interpret deps dispatch (PromptForPattern Select)

    Assert.Empty sent

[<Fact>]
let ``a chosen root is dispatched as an opened path`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with ChooseOption = fun _ _ -> Some(at "elsewhere") }

    Shell.interpret deps dispatch (PromptForRoot Right)

    Assert.Equal<Message list>([ PathOpened(Right, fp (at "elsewhere")) ], List.ofSeq sent)

[<Fact>]
let ``the chooser is offered the roots the platform was given`` () =
    let _, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddDirectory(at "home/user")
    let mutable offered = []

    let deps =
        { noDeps () with
            FileSystem = fs
            Roots = RootAndHome(fp (at "home/user"))
            ChooseOption = fun _ roots -> offered <- roots; None }

    Shell.interpret deps dispatch (PromptForRoot Left)

    Assert.Equal<string list>([ at ""; at "home/user" ], offered)

[<WindowsFact>]
let ``the chooser is offered every mounted drive`` () =
    let _, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"
    fs.AddDirectory @"D:\data"
    let mutable offered = []

    let deps =
        { noDeps () with
            FileSystem = fs
            ChooseOption = fun _ drives -> offered <- drives; None }

    Shell.interpret deps dispatch (PromptForRoot Left)

    Assert.Equal<string list>([ @"C:\"; @"D:\" ], offered)

[<Fact>]
let ``a listing error is reported under the listing title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with ReportError = fun title message -> reported <- (title, message) }

    Shell.interpret deps ignore (ReportListingError(DirectoryListing.DirectoryNotFound(fp (at "gone"))))

    Assert.Equal(ListingErrorMessage.Title, fst reported)
    Assert.Contains(at "gone", snd reported)

[<Fact>]
let ``an operation error is reported under the operation title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with ReportError = fun title message -> reported <- (title, message) }

    Shell.interpret deps ignore (ReportOperationError(TargetExists(fp (at "start/docs"))))

    Assert.Equal(OperationErrorMessage.Title, fst reported)
    Assert.Contains("docs", snd reported)

[<Fact>]
let ``a confirmed delete is dispatched back`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with Confirm = fun _ _ -> true }

    Shell.interpret deps dispatch (ConfirmDelete(deleteRequest [ en "notes.txt" ] NamedEntriesOnly))

    Assert.Equal<Message list>([ DeleteConfirmed ToTrash ], List.ofSeq sent)

[<Fact>]
let ``a refused delete dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (ConfirmDelete(deleteRequest [ en "notes.txt" ] NamedEntriesOnly))

    Assert.Empty sent

[<Fact>]
let ``the delete question carries its own title and the warning`` () =
    let _, dispatch = capture ()
    let mutable asked = ("", "")
    let deps = { noDeps () with Confirm = fun title message -> asked <- (title, message); false }

    Shell.interpret deps dispatch (ConfirmDelete(deleteRequest [ en "docs" ] WithDirectoryContents))

    Assert.Equal(DeleteConfirmation.Title, fst asked)
    Assert.Contains(DeleteConfirmation.TrashWarning, snd asked)

[<Fact>]
let ``a permanent delete asks under its own title`` () =
    let _, dispatch = capture ()
    let mutable asked = ("", "")
    let deps = { noDeps () with Confirm = fun title message -> asked <- (title, message); false }

    let request =
        { deleteRequest [ en "notes.txt" ] NamedEntriesOnly with Mode = Permanently }

    Shell.interpret deps dispatch (ConfirmDelete request)

    Assert.Equal(DeleteConfirmation.PermanentTitle, fst asked)

[<Fact>]
let ``a confirmed permanent delete is dispatched back with its mode`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with Confirm = fun _ _ -> true }

    let request =
        { deleteRequest [ en "notes.txt" ] NamedEntriesOnly with Mode = Permanently }

    Shell.interpret deps dispatch (ConfirmDelete request)

    Assert.Equal<Message list>([ DeleteConfirmed Permanently ], List.ofSeq sent)

[<Fact>]
let ``targets sent to the trash go through the port, not the file system`` () =
    let sent, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    let trashed = ResizeArray<FileSystemPath>()

    let deps =
        { Fakes.dependencies fs (at "start") with
            SendToTrash = fun path -> trashed.Add path; Ok() }

    Shell.interpret deps dispatch (DeleteTargets([ fp (at "start/notes.txt") ], ToTrash))

    Assert.Equal<FileSystemPath list>([ fp (at "start/notes.txt") ], List.ofSeq trashed)
    Assert.True(fs.File.Exists(at "start/notes.txt"))

    Assert.Equal<Message list>(
        [ DeleteFinished [ { Name = en "notes.txt"; Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``a permanent delete removes the target itself`` () =
    let sent, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")

    let deps =
        { Fakes.dependencies fs (at "start") with
            SendToTrash = fun path -> failwith $"the trash was asked for {FileSystemPath.value path}" }

    Shell.interpret deps dispatch (DeleteTargets([ fp (at "start/notes.txt") ], Permanently))

    Assert.False(fs.File.Exists(at "start/notes.txt"))

    Assert.Equal<Message list>(
        [ DeleteFinished [ { Name = en "notes.txt"; Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``what the trash refused is reported item by item`` () =
    let sent, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    let refusal = AccessDenied(fp (at "start/notes.txt"))

    let deps =
        { Fakes.dependencies fs (at "start") with
            SendToTrash = fun _ -> Error refusal }

    Shell.interpret deps dispatch (DeleteTargets([ fp (at "start/notes.txt") ], ToTrash))

    Assert.Equal<Message list>(
        [ DeleteFinished [ { Name = en "notes.txt"; Outcome = Error refusal } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``item failures are reported under the operation title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with ReportError = fun title message -> reported <- (title, message) }

    let failed =
        [ { Name = EntryName.create "gone.txt"
            Outcome = Error(SourceNotFound(fp (at "start/gone.txt"))) } ]

    Shell.interpret deps ignore (ReportItemFailures failed)

    Assert.Equal(OperationErrorMessage.Title, fst reported)
    Assert.Contains("gone.txt", snd reported)

let private copyReadyFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "new")
    fs.AddDirectory(at "backup")
    fs

[<Fact>]
let ``copy targets are copied and the outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = copyReadyFs ()
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (CopyTargets([ fp (at "start/notes.txt") ], fp (at "backup")))

    Assert.Equal("new", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal<Message list>(
        [ CopyFinished [ { Name = EntryName.create "notes.txt"; Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``a batch is handed to the runner under its own title`` () =
    let _, dispatch = capture ()
    let titles = ResizeArray<string>()
    let deps () =
        { noDeps () with
            FileSystem = copyReadyFs ()
            RunBatch = fun title work finish -> titles.Add title; work Operations.unattended |> finish }

    Shell.interpret (deps ()) dispatch (CopyTargets([ fp (at "start/notes.txt") ], fp (at "backup")))
    Shell.interpret (deps ()) dispatch (MoveTargets([ fp (at "start/notes.txt") ], fp (at "backup")))

    Assert.Equal<string list>([ Shell.CopyTitle; Shell.MoveTitle ], List.ofSeq titles)

[<Fact>]
let ``nothing is run when every collision is refused`` () =
    let sent, dispatch = capture ()
    let fs = copyReadyFs ()
    fs.AddFile(at "backup/notes.txt", MockFileData "old")
    let mutable started = false
    let deps =
        { noDeps () with
            FileSystem = fs
            AskOverwrite = (fun _ _ -> Operations.No)
            RunBatch = fun _ work finish -> started <- true; work Operations.unattended |> finish }

    Shell.interpret deps dispatch (CopyTargets([ fp (at "start/notes.txt") ], fp (at "backup")))

    Assert.False started
    Assert.Empty sent
    Assert.Equal("old", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``a collision asks the overwrite question under its own title`` () =
    let _, dispatch = capture ()
    let fs = copyReadyFs ()
    fs.AddFile(at "backup/notes.txt", MockFileData "old")
    let mutable asked = ("", "")
    let deps =
        { noDeps () with
            FileSystem = fs
            AskOverwrite = fun title message -> asked <- (title, message); Operations.No }

    Shell.interpret deps dispatch (CopyTargets([ fp (at "start/notes.txt") ], fp (at "backup")))

    Assert.Equal(OverwriteQuestion.Title, fst asked)
    Assert.Contains(at "backup/notes.txt", snd asked)
    Assert.Equal("old", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``the target prompt suggests a path and carries back what was typed`` () =
    let sent, dispatch = capture ()
    let mutable offered = ("", "")
    let deps =
        { noDeps () with
            AskTarget = fun title suggestion -> offered <- (title, suggestion); Some "readme.txt" }

    Shell.interpret deps dispatch (PromptForTarget(fp (at "backup")))

    Assert.Equal(Shell.MovePrompt, fst offered)
    Assert.Equal(at "backup", snd offered)
    Assert.Equal<Message list>([ TargetEntered "readme.txt" ], List.ofSeq sent)

[<Fact>]
let ``a cancelled target prompt dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (PromptForTarget(fp (at "backup")))

    Assert.Empty sent

[<Fact>]
let ``move targets are moved and the outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = copyReadyFs ()
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (MoveTargets([ fp (at "start/notes.txt") ], fp (at "backup")))

    Assert.Equal("new", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.False(fs.File.Exists(at "start/notes.txt"))
    Assert.Equal<Message list>(
        [ MoveFinished [ { Name = EntryName.create "notes.txt"; Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``a rename is carried out and its outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = copyReadyFs ()
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (RenameTarget(fp (at "start/notes.txt"), en "readme.txt"))

    Assert.Equal("new", fs.File.ReadAllText(at "start/readme.txt"))
    Assert.Equal<Message list>([ RenameFinished(en "readme.txt", Ok()) ], List.ofSeq sent)

[<Fact>]
let ``a viewed file is handed over opened under its own path`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "first\r\nsecond")
    let mutable shown = ("", [])

    let readOut (source: FileWindow.Source) =
        let index = LineIndex.Index source
        index.ScanAll()
        let lines = [ for line in 0 .. index.Known - 1 -> index.Text line |> Option.defaultValue "" ]
        shown <- (FileSystemPath.value source.Path, lines)

    let deps = { noDeps () with FileSystem = fs; ShowFile = readOut }

    Shell.interpret deps ignore (ViewFile(fp (at "start/notes.txt")))

    Assert.Equal(at "start/notes.txt", fst shown)
    Assert.Equal<string list>([ "first"; "second" ], snd shown)

[<Fact>]
let ``a file that cannot be read is reported instead of shown`` () =
    let mutable shown = false
    let mutable reported = ("", "")

    let deps =
        { noDeps () with
            ShowFile = fun _ -> shown <- true
            ReportError = fun title message -> reported <- (title, message) }

    Shell.interpret deps ignore (ViewFile(fp (at "start/gone.txt")))

    Assert.False shown
    Assert.Equal(ContentErrorMessage.Title, fst reported)
    Assert.Contains("gone.txt", snd reported)

[<Fact>]
let ``an edited file is handed to the editor and its outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let edited = ResizeArray<FileSystemPath>()
    let deps = { noDeps () with RunEditor = fun _ path -> edited.Add path; Ok() }

    Shell.interpret deps dispatch (EditFile(fp (at "start/notes.txt"), None))

    Assert.Equal(fp (at "start/notes.txt"), Assert.Single edited)
    Assert.Equal<Message list>([ EditFinished(Ok()) ], List.ofSeq sent)

[<Fact>]
let ``the editor the effect names is the one that is started`` () =
    let _, dispatch = capture ()
    let started = ResizeArray<EditorPath option>()
    let deps = { noDeps () with RunEditor = fun chosen _ -> started.Add chosen; Ok() }
    let chosen = EditorPath.tryCreate "code.exe"

    Shell.interpret deps dispatch (EditFile(fp (at "start/notes.txt"), chosen))

    Assert.Equal(chosen, Assert.Single started)

[<Fact>]
let ``an editor that fails dispatches its failure back`` () =
    let sent, dispatch = capture ()
    let failure = OperationFailed(fp (at "start/notes.txt"), "no editor")
    let deps = { noDeps () with RunEditor = fun _ _ -> Error failure }

    Shell.interpret deps dispatch (EditFile(fp (at "start/notes.txt"), None))

    Assert.Equal<Message list>([ EditFinished(Error failure) ], List.ofSeq sent)

[<Fact>]
let ``a name for a new file is dispatched as FileNameEntered`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with AskName = fun _ _ -> Some "draft.md" }

    Shell.interpret deps dispatch PromptForFileName

    Assert.Equal<Message list>([ FileNameEntered(en "draft.md") ], List.ofSeq sent)

[<Fact>]
let ``a cancelled new file prompt dispatches nothing`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with AskName = fun _ _ -> None }

    Shell.interpret deps dispatch PromptForFileName

    Assert.Empty sent

[<Fact>]
let ``the new file prompt opens on nothing`` () =
    let offered = ResizeArray<string>()
    let deps = { noDeps () with AskName = fun _ suggestion -> offered.Add suggestion; None }

    Shell.interpret deps ignore PromptForFileName

    Assert.Equal("", Assert.Single offered)

[<Fact>]
let ``changed properties are dispatched back`` () =
    let sent, dispatch = capture ()
    let shown =
        { Path = fp (at "start/docs")
          Flags = Set.empty
          Created = DateTimeOffset(DateTime(2024, 1, 1))
          Modified = DateTimeOffset(DateTime(2024, 1, 1))
          Accessed = DateTimeOffset(DateTime(2024, 1, 1)) }
    let changed = EntryProperties.setFlag HiddenFlag true shown
    let deps = { noDeps () with EditProperties = fun _ -> Some changed }

    Shell.interpret deps dispatch (ShowProperties shown)

    Assert.Equal<Message list>([ PropertiesEntered changed ], List.ofSeq sent)

[<Fact>]
let ``a dismissed properties dialog dispatches nothing`` () =
    let sent, dispatch = capture ()
    let shown =
        { Path = fp (at "start/docs")
          Flags = Set.empty
          Created = DateTimeOffset(DateTime(2024, 1, 1))
          Modified = DateTimeOffset(DateTime(2024, 1, 1))
          Accessed = DateTimeOffset(DateTime(2024, 1, 1)) }

    Shell.interpret (noDeps ()) dispatch (ShowProperties shown)

    Assert.Empty sent

[<Fact>]
let ``changed editors are dispatched back`` () =
    let sent, dispatch = capture ()
    let changed =
        { EditorSettings.none with
            DefaultEditor = EditorPath.tryCreate "code.exe" }
    let deps = { noDeps () with EditEditors = fun _ _ -> Some changed }

    Shell.interpret deps dispatch (ShowSettings(EditorSettings.none, None))

    Assert.Equal<Message list>([ EditorsEntered changed ], List.ofSeq sent)

[<Fact>]
let ``the executable the effect names is offered to the settings popup`` () =
    let offered = ResizeArray<EditorPath option>()
    let deps = { noDeps () with EditEditors = fun _ candidate -> offered.Add candidate; None }
    let candidate = EditorPath.tryCreate (at "start/rider64.exe")

    Shell.interpret deps ignore (ShowSettings(EditorSettings.none, candidate))

    Assert.Equal(candidate, Assert.Single offered)

[<Fact>]
let ``a dismissed settings dialog dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (ShowSettings(EditorSettings.none, None))

    Assert.Empty sent

[<Fact>]
let ``saved settings are written where the settings path points`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    let settings = Settings.forPath (fp (at "start"))
    let deps = { noDeps () with FileSystem = fs; SettingsPath = fp (at "config/marbas.conf") }

    Shell.interpret deps ignore (SaveSettings settings)

    Assert.Equal(settings, Settings.load fs settings (fp (at "config/marbas.conf")))

[<Fact>]
let ``quit runs the quit port`` () =
    let mutable quit = false
    let deps = { noDeps () with OnQuit = fun () -> quit <- true }

    Shell.interpret deps ignore Quit

    Assert.True quit

[<Fact>]
let ``the name prompt offers the name that was remembered`` () =
    let _, dispatch = capture ()
    let mutable offered = ""
    let deps = { noDeps () with AskName = fun _ suggestion -> offered <- suggestion; None }

    Shell.interpret deps dispatch (PromptForName(Some(en "work")))

    Assert.Equal("work", offered)

[<Fact>]
let ``the name prompt starts empty when nothing was remembered`` () =
    let _, dispatch = capture ()
    let mutable offered = "something"
    let deps = { noDeps () with AskName = fun _ suggestion -> offered <- suggestion; None }

    Shell.interpret deps dispatch (PromptForName None)

    Assert.Equal("", offered)

let private packReadyFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "new")
    fs.AddDirectory(at "backup")
    fs

let private zipFormat = ArchiveFormat.ofKind Zip |> Expect.some

let private packing path =
    { Path = fp path
      Format = zipFormat
      Compression = Normal
      Password = None }

let private choice =
    { Target = at "backup/notes.zip"
      Format = zipFormat
      Compression = Maximum
      Password = None }

[<Fact>]
let ``the archive prompt offers the suggestion and carries back the choice`` () =
    let sent, dispatch = capture ()
    let mutable offered = ""

    let deps =
        { noDeps () with
            AskArchive =
                fun suggestion ->
                    offered <- suggestion
                    Some choice }

    Shell.interpret deps dispatch (PromptForArchive(fp (at "backup/notes.zip")))

    Assert.Equal(at "backup/notes.zip", offered)
    Assert.Equal<Message list>([ ArchiveEntered choice ], List.ofSeq sent)

[<Fact>]
let ``a cancelled archive prompt dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (PromptForArchive(fp (at "backup/notes.zip")))

    Assert.Empty sent

[<Fact>]
let ``archive targets are packed and the outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = packReadyFs ()
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (ArchiveTargets([ fp (at "start/notes.txt") ], packing (at "backup/notes.zip")))

    Assert.True(fs.File.Exists(at "backup/notes.zip"))

    Assert.Equal<Message list>(
        [ PackFinished(en "notes.zip", [ { Name = en "notes.txt"; Outcome = Ok() } ]) ],
        List.ofSeq sent
    )

[<Fact>]
let ``packing runs under its own title`` () =
    let _, dispatch = capture ()
    let titles = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = packReadyFs ()
            RunBatch =
                fun title work finish ->
                    titles.Add title
                    work Operations.unattended |> finish }

    Shell.interpret deps dispatch (ArchiveTargets([ fp (at "start/notes.txt") ], packing (at "backup/notes.zip")))

    Assert.Equal<string list>([ Shell.PackTitle ], List.ofSeq titles)

[<Fact>]
let ``an archive that already exists is asked about first`` () =
    let _, dispatch = capture ()
    let fs = packReadyFs ()
    fs.AddFile(at "backup/notes.zip", MockFileData "old")
    let mutable asked = ("", "")

    let deps =
        { noDeps () with
            FileSystem = fs
            Confirm =
                fun title message ->
                    asked <- (title, message)
                    false }

    Shell.interpret deps dispatch (ArchiveTargets([ fp (at "start/notes.txt") ], packing (at "backup/notes.zip")))

    Assert.Equal(OverwriteQuestion.Title, fst asked)
    Assert.Contains(at "backup/notes.zip", snd asked)
    Assert.Equal("old", fs.File.ReadAllText(at "backup/notes.zip"))

[<Fact>]
let ``a refused overwrite packs nothing`` () =
    let sent, dispatch = capture ()
    let fs = packReadyFs ()
    fs.AddFile(at "backup/notes.zip", MockFileData "old")
    let deps = { noDeps () with FileSystem = fs; Confirm = fun _ _ -> false }

    Shell.interpret deps dispatch (ArchiveTargets([ fp (at "start/notes.txt") ], packing (at "backup/notes.zip")))

    Assert.Empty sent

[<Fact>]
let ``an accepted overwrite replaces the archive`` () =
    let sent, dispatch = capture ()
    let fs = packReadyFs ()
    fs.AddFile(at "backup/notes.zip", MockFileData "old")
    let deps = { noDeps () with FileSystem = fs; Confirm = fun _ _ -> true }

    Shell.interpret deps dispatch (ArchiveTargets([ fp (at "start/notes.txt") ], packing (at "backup/notes.zip")))

    let written = fs.File.ReadAllBytes(at "backup/notes.zip")

    Assert.Equal<byte list>([ 0x50uy; 0x4Buy ], written |> Array.truncate 2 |> List.ofArray)
    Assert.Single sent |> ignore

let private unpackReadyFs password =
    let fs = MockFileSystem()
    fs.AddFile(at "start/work/notes.txt", MockFileData "notes")
    fs.AddFile(at "start/work/docs/inner.txt", MockFileData "inner")
    fs.AddDirectory(at "backup")

    Archive.write
        fs
        unattended
        { Path = fp (at "start/data.zip")
          Format = zipFormat
          Compression = Normal
          Password = password }
        [ fp (at "start/work/notes.txt"); fp (at "start/work/docs") ]
    |> ignore

    fs

[<Fact>]
let ``an archive is emptied into the target directory and the outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs None
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.zip") ], fp (at "backup")))

    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal("inner", fs.File.ReadAllText(at "backup/docs/inner.txt"))

    Assert.Equal<Message list>(
        [ UnpackFinished(
              [ { Name = en "notes.txt"; Outcome = Ok() }
                { Name = en "docs"; Outcome = Ok() } ]
          ) ],
        List.ofSeq sent
    )

[<Fact>]
let ``unpacking runs under its own title`` () =
    let _, dispatch = capture ()
    let titles = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = unpackReadyFs None
            RunBatch =
                fun title work finish ->
                    titles.Add title
                    work Operations.unattended |> finish }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.zip") ], fp (at "backup")))

    Assert.Equal<string list>([ Shell.UnpackTitle ], List.ofSeq titles)

[<Fact>]
let ``an entry already in the target directory is asked about`` () =
    let _, dispatch = capture ()
    let fs = unpackReadyFs None
    fs.AddFile(at "backup/notes.txt", MockFileData "old")
    let asked = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = fs
            AskOverwrite =
                fun _ message ->
                    asked.Add message
                    No }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.zip") ], fp (at "backup")))

    Assert.Equal(1, asked.Count)
    Assert.Contains(at "backup/notes.txt", asked[0])
    Assert.Equal("old", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal("inner", fs.File.ReadAllText(at "backup/docs/inner.txt"))

[<Fact>]
let ``a protected archive is asked for its password once`` () =
    let fs = unpackReadyFs (ArchivePassword.tryCreate "letmein")
    let _, dispatch = capture ()
    let asked = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = fs
            AskPassword =
                fun _ label ->
                    asked.Add label
                    Some "letmein" }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.zip") ], fp (at "backup")))

    Assert.Equal(1, asked.Count)
    Assert.Contains("data.zip", asked[0])
    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``an archive that needs no password is never asked for one`` () =
    let _, dispatch = capture ()
    let mutable asked = false

    let deps =
        { noDeps () with
            FileSystem = unpackReadyFs None
            AskPassword =
                fun _ _ ->
                    asked <- true
                    None }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.zip") ], fp (at "backup")))

    Assert.False asked

[<Fact>]
let ``a dismissed password prompt leaves the archive alone`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs (ArchivePassword.tryCreate "letmein")
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.zip") ], fp (at "backup")))

    Assert.Empty sent
    Assert.Empty(fs.Directory.GetFiles(at "backup"))

[<Fact>]
let ``an archive that cannot be read answers for itself`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs None
    fs.AddFile(at "start/data.tar", MockFileData "whatever")
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.tar") ], fp (at "backup")))

    Assert.Equal<Message list>(
        [ UnpackFinished(
              [ { Name = en "data.tar"
                  Outcome = Error(ArchiveFailed(KindCannotBeRead(fp (at "start/data.tar")))) } ]
          ) ],
        List.ofSeq sent
    )

[<Fact>]
let ``a password prompt answered with nothing leaves the archive alone`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs (ArchivePassword.tryCreate "letmein")

    let deps =
        { noDeps () with
            FileSystem = fs
            AskPassword = fun _ _ -> Some "" }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.zip") ], fp (at "backup")))

    Assert.Empty sent
    Assert.Empty(fs.Directory.GetFiles(at "backup"))

[<Fact>]
let ``an entry of an archive is handed over under the name of its place in it`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "first\r\nsecond")

    Archive.write
        fs
        Operations.unattended
        { Path = fp (at "backup/data.zip")
          Format = ArchiveFormat.ofKind Zip |> Expect.some
          Compression = Normal
          Password = None }
        [ fp (at "work/notes.txt") ]
    |> ignore

    let mutable shown = ("", [])

    let readOut (source: FileWindow.Source) =
        let index = LineIndex.Index source
        index.ScanAll()
        let lines = [ for line in 0 .. index.Known - 1 -> index.Text line |> Option.defaultValue "" ]
        shown <- (FileSystemPath.value source.Path, lines)

    let deps = { noDeps () with FileSystem = fs; ShowFile = readOut }
    let entry = ArchivePath.tryCreate "notes.txt" |> Expect.some

    Shell.interpret deps ignore (ViewArchiveEntry(fp (at "backup/data.zip"), entry))

    Assert.Equal(at "backup/data.zip/notes.txt", fst shown)
    Assert.Equal<string list>([ "first"; "second" ], snd shown)

[<Fact>]
let ``an entry that cannot be read is reported under the archive title`` () =
    let mutable shown = false
    let mutable reported = ("", "")
    let entry = ArchivePath.tryCreate "notes.txt" |> Expect.some

    let deps =
        { noDeps () with
            ShowFile = fun _ -> shown <- true
            ReportError = fun title message -> reported <- (title, message) }

    Shell.interpret deps ignore (ViewArchiveEntry(fp (at "backup/gone.zip"), entry))

    Assert.False shown
    Assert.Equal(ArchiveErrorMessage.Title, fst reported)
    Assert.Contains("gone.zip", snd reported)

[<Fact>]
let ``a locked archive asks for its password before an entry is shown`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "secret text")

    Archive.write
        fs
        Operations.unattended
        { Path = fp (at "backup/locked.zip")
          Format = ArchiveFormat.ofKind Zip |> Expect.some
          Compression = Normal
          Password = ArchivePassword.tryCreate "opensesame" }
        [ fp (at "work/notes.txt") ]
    |> ignore

    let mutable asked = 0
    let mutable shown = ""
    let entry = ArchivePath.tryCreate "notes.txt" |> Expect.some

    let deps =
        { noDeps () with
            FileSystem = fs
            AskPassword = fun _ _ -> asked <- asked + 1; Some "opensesame"
            ShowFile = fun source -> shown <- Text.Encoding.ASCII.GetString(source.Read(0L, 6)) }

    Shell.interpret deps ignore (ViewArchiveEntry(fp (at "backup/locked.zip"), entry))

    Assert.Equal(1, asked)
    Assert.Equal("secret", shown)

[<Fact>]
let ``a locked archive whose password is not given shows nothing`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "backup")
    fs.AddFile(at "work/notes.txt", MockFileData "secret text")

    Archive.write
        fs
        Operations.unattended
        { Path = fp (at "backup/locked.zip")
          Format = ArchiveFormat.ofKind Zip |> Expect.some
          Compression = Normal
          Password = ArchivePassword.tryCreate "opensesame" }
        [ fp (at "work/notes.txt") ]
    |> ignore

    let mutable shown = false
    let entry = ArchivePath.tryCreate "notes.txt" |> Expect.some

    let deps =
        { noDeps () with
            FileSystem = fs
            AskPassword = fun _ _ -> None
            ShowFile = fun _ -> shown <- true }

    Shell.interpret deps ignore (ViewArchiveEntry(fp (at "backup/locked.zip"), entry))

    Assert.False shown

let private entry raw = ArchivePath.tryCreate raw |> Expect.some

[<Fact>]
let ``entries copied out of an archive land in the target directory and the outcome is dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs None
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret
        deps
        dispatch
        (ExtractEntries(fp (at "start/data.zip"), [ entry "notes.txt"; entry "docs" ], fp (at "backup")))

    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal("inner", fs.File.ReadAllText(at "backup/docs/inner.txt"))

    Assert.Equal<Message list>(
        [ CopyFinished(
              [ { Name = en "notes.txt"; Outcome = Ok() }
                { Name = en "docs"; Outcome = Ok() } ]
          ) ],
        List.ofSeq sent
    )

[<Fact>]
let ``an entry deeper in an archive is copied out under its own name`` () =
    let _, dispatch = capture ()
    let fs = unpackReadyFs None
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (ExtractEntries(fp (at "start/data.zip"), [ entry "docs/inner.txt" ], fp (at "backup")))

    Assert.Equal("inner", fs.File.ReadAllText(at "backup/inner.txt"))

[<Fact>]
let ``copying out of an archive runs under the copying title and leaves the archive as it was`` () =
    let _, dispatch = capture ()
    let fs = unpackReadyFs None
    let before = fs.File.ReadAllBytes(at "start/data.zip")
    let titles = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = fs
            RunBatch =
                fun title work finish ->
                    titles.Add title
                    work Operations.unattended |> finish }

    Shell.interpret deps dispatch (ExtractEntries(fp (at "start/data.zip"), [ entry "notes.txt" ], fp (at "backup")))

    Assert.Equal<string list>([ Shell.CopyTitle ], List.ofSeq titles)
    Assert.Equal<byte list>(List.ofArray before, List.ofArray (fs.File.ReadAllBytes(at "start/data.zip")))

[<Fact>]
let ``an entry copied over one already there is asked about by its landing place`` () =
    let _, dispatch = capture ()
    let fs = unpackReadyFs None
    fs.AddFile(at "backup/notes.txt", MockFileData "old")
    let asked = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = fs
            AskOverwrite =
                fun _ message ->
                    asked.Add message
                    Operations.No }

    Shell.interpret deps dispatch (ExtractEntries(fp (at "start/data.zip"), [ entry "notes.txt" ], fp (at "backup")))

    Assert.Contains(at "backup/notes.txt", Assert.Single asked)
    Assert.Equal("old", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``a refused overwrite copies nothing and starts no batch`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs None
    fs.AddFile(at "backup/notes.txt", MockFileData "old")
    let batches = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = fs
            AskOverwrite = fun _ _ -> Operations.No
            RunBatch =
                fun title work finish ->
                    batches.Add title
                    work Operations.unattended |> finish }

    Shell.interpret deps dispatch (ExtractEntries(fp (at "start/data.zip"), [ entry "notes.txt" ], fp (at "backup")))

    Assert.Empty batches
    Assert.Empty sent

[<Fact>]
let ``copying out of a locked archive asks for the password once`` () =
    let _, dispatch = capture ()
    let fs = unpackReadyFs (ArchivePassword.tryCreate "opensesame")
    let asked = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = fs
            AskPassword =
                fun _ message ->
                    asked.Add message
                    Some "opensesame" }

    Shell.interpret deps dispatch (ExtractEntries(fp (at "start/data.zip"), [ entry "notes.txt" ], fp (at "backup")))

    Assert.Single asked |> ignore
    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``copying out of a locked archive whose password is not given copies nothing`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs (ArchivePassword.tryCreate "opensesame")
    let deps = { noDeps () with FileSystem = fs; AskPassword = fun _ _ -> None }

    Shell.interpret deps dispatch (ExtractEntries(fp (at "start/data.zip"), [ entry "notes.txt" ], fp (at "backup")))

    Assert.False(fs.File.Exists(at "backup/notes.txt"))
    Assert.Empty sent
