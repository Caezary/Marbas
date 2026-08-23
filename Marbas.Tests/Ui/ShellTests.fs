module Marbas.Tests.Ui.ShellTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui
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
    let deps = { noDeps () with RunEditor = fun path -> edited.Add path; Ok() }

    Shell.interpret deps dispatch (EditFile(fp (at "start/notes.txt")))

    Assert.Equal(fp (at "start/notes.txt"), Assert.Single edited)
    Assert.Equal<Message list>([ EditFinished(Ok()) ], List.ofSeq sent)

[<Fact>]
let ``an editor that fails dispatches its failure back`` () =
    let sent, dispatch = capture ()
    let failure = OperationFailed(fp (at "start/notes.txt"), "no editor")
    let deps = { noDeps () with RunEditor = fun _ -> Error failure }

    Shell.interpret deps dispatch (EditFile(fp (at "start/notes.txt")))

    Assert.Equal<Message list>([ EditFinished(Error failure) ], List.ofSeq sent)

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
