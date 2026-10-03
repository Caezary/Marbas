module Marbas.Tests.Ui.App.ShellTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui.Messages
open Marbas.Ui.App
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private entry raw = ArchivePath.tryCreate raw |> Expect.some

let private dataZip = fp (at "start/data.zip")

let private noDeps () = Fakes.dependencies (MockFileSystem()) (at "start")

let private launching target =
    { Target = fp target
      Program = None
      WorkingDirectory = fp (at "start")
      Elevation = AsUser }

let private deleteRequest names scope =
    { Targets = names
      Scope = scope
      Mode = ToTrash }

let private cursor side index = { Side = side; Index = index }

let private capture () =
    let sent = ResizeArray<Message>()
    sent, fun (msg: Message) -> sent.Add msg

let private recording (reported: ResizeArray<FileSystemPath>) =
    { Report = reported.Add
      IsCancelled = fun () -> false }

let private unwrittenHistory msg =
    match msg with
    | HistoryUnwritten(HistoryAccessDenied path)
    | HistoryUnwritten(HistoryFailed(path, _)) -> Some path
    | _ -> None

let private stopped =
    { Report = ignore
      IsCancelled = fun () -> true }

[<Fact>]
let ``a supplied name is dispatched as NameEntered`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with Prompts.AskName = fun _ -> Some "work" }

    Shell.interpret deps dispatch PromptForName

    Assert.Equal<Message list>([ NameEntered(en "work") ], List.ofSeq sent)

[<Fact>]
let ``a cancelled name prompt dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch PromptForName

    Assert.Empty sent

[<Fact>]
let ``the name prompt uses the mkdir label`` () =
    let _, dispatch = capture ()
    let mutable asked = ""
    let deps = { noDeps () with Prompts.AskName = fun request -> asked <- request.Title; None }

    Shell.interpret deps dispatch PromptForName

    Assert.Equal(Shell.MkdirPrompt, asked)

[<Fact>]
let ``a selecting pattern uses the select label and carries the answer back`` () =
    let sent, dispatch = capture ()
    let mutable asked = ""
    let deps =
        { noDeps () with
            Prompts.AskPattern = fun request -> asked <- request.Title; Some "*.fs" }

    Shell.interpret deps dispatch (PromptForPattern Select)

    Assert.Equal(Shell.SelectPrompt, asked)
    Assert.Contains(PatternEntered(Select, NamePattern.tryCreate "*.fs" |> Expect.some), sent)

[<Fact>]
let ``a deselecting pattern uses the deselect label`` () =
    let _, dispatch = capture ()
    let mutable asked = ""
    let deps = { noDeps () with Prompts.AskPattern = fun request -> asked <- request.Title; None }

    Shell.interpret deps dispatch (PromptForPattern Deselect)

    Assert.Equal(Shell.DeselectPrompt, asked)

[<Fact>]
let ``a chosen drive is dispatched with the side it was asked for`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with Prompts.ChooseDrive = Fakes.choosingDrive (at "elsewhere") }

    Shell.interpret deps dispatch (PromptForRoot Right)

    Assert.Equal<Message list>([ DriveChosen(Right, MountedDrive(Fakes.driveAt (at "elsewhere"))) ], List.ofSeq sent)

[<Fact>]
let ``a drive handed over is dispatched without a side`` () =
    let sent, dispatch = capture ()
    let stick = MountedDrive(Fakes.driveAt (at "media/stick"))
    let deps = { noDeps () with Prompts.ChooseDrive = fun _ -> Some(HandOver stick) }

    Shell.interpret deps dispatch (PromptForRoot Left)

    Assert.Equal<Message list>([ DriveHandedOver stick ], List.ofSeq sent)

[<Fact>]
let ``a chosen home directory is dispatched as it was chosen`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with Prompts.ChooseDrive = fun _ -> Some(StandOn(HomeDirectory(fp (at "home/user")))) }

    Shell.interpret deps dispatch (PromptForRoot Left)

    Assert.Equal<Message list>([ DriveChosen(Left, HomeDirectory(fp (at "home/user"))) ], List.ofSeq sent)

[<Fact>]
let ``the chooser is told the side of the panel it was asked for`` () =
    let _, dispatch = capture ()
    let asked = ResizeArray<PanelSide>()
    let deps = { noDeps () with Prompts.ChooseDrive = fun request -> asked.Add request.Side; None }

    Shell.interpret deps dispatch (PromptForRoot Left)
    Shell.interpret deps dispatch (PromptForRoot Right)

    Assert.Equal<PanelSide list>([ Left; Right ], List.ofSeq asked)

[<Fact>]
let ``the chooser is offered every drive row as it was listed`` () =
    let _, dispatch = capture ()
    let mutable offered = []

    let stick =
        { Root = fp (at "media/stick")
          Label = Some "STICK"
          Kind = Removable
          Size = 64L
          Free = 16L }

    let rows = [ MountedDrive stick; HomeDirectory(fp (at "home/user")) ]

    let deps =
        { noDeps () with
            Processes.ListDrives = fun () -> rows
            Prompts.ChooseDrive = fun offering -> offered <- offering.Drives; None }

    Shell.interpret deps dispatch (PromptForRoot Left)

    Assert.Equal<DriveRow list>(rows, offered)

[<Fact>]
let ``closing the chooser dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (PromptForRoot Left)

    Assert.Empty sent

[<Fact>]
let ``the drives are listed again each time the chooser opens`` () =
    let _, dispatch = capture ()
    let mutable listings = 0

    let deps =
        { noDeps () with
            Processes.ListDrives = fun () -> listings <- listings + 1; [] }

    Shell.interpret deps dispatch (PromptForRoot Left)
    Shell.interpret deps dispatch (PromptForRoot Right)

    Assert.Equal(2, listings)

[<Fact>]
let ``a listing error is reported under the listing title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    Shell.interpret deps ignore (ReportListingError(DirectoryListing.DirectoryNotFound(fp (at "gone"))))

    Assert.Equal(ListingErrorMessage.Title, fst reported)
    Assert.Contains(at "gone", snd reported)

[<Fact>]
let ``archives that cannot be read are reported in one message under the archive title`` () =
    let reported = ResizeArray<string * string>()
    let deps = { noDeps () with Prompts.ReportError = fun report -> reported.Add(report.Title, report.Message) }
    let other = fp (at "start/other.zip")

    Shell.interpret deps ignore (ReportUnverifiedHits [ ArchiveDamaged(dataZip, "bad header"); ArchiveDamaged(other, "bad header") ])

    let title, message = Assert.Single reported
    Assert.Equal(ArchiveErrorMessage.Title, title)
    Assert.Contains(at "start/data.zip", message)
    Assert.Contains(at "start/other.zip", message)

[<Fact>]
let ``an operation error is reported under the operation title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    Shell.interpret deps ignore (ReportOperationError(TargetExists(fp (at "start/docs"))))

    Assert.Equal(OperationErrorMessage.Title, fst reported)
    Assert.Contains("docs", snd reported)

[<Fact>]
let ``a confirmed delete is dispatched back`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with Prompts.Confirm = fun _ -> true }

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
    let deps = { noDeps () with Prompts.Confirm = fun question -> asked <- (question.Title, question.Question); false }

    Shell.interpret deps dispatch (ConfirmDelete(deleteRequest [ en "docs" ] WithDirectoryContents))

    Assert.Equal(DeleteConfirmation.Title, fst asked)
    Assert.Contains(DeleteConfirmation.TrashWarning, snd asked)

[<Fact>]
let ``a permanent delete asks under its own title`` () =
    let _, dispatch = capture ()
    let mutable asked = ("", "")
    let deps = { noDeps () with Prompts.Confirm = fun question -> asked <- (question.Title, question.Question); false }

    let request =
        { deleteRequest [ en "notes.txt" ] NamedEntriesOnly with Mode = Permanently }

    Shell.interpret deps dispatch (ConfirmDelete request)

    Assert.Equal(DeleteConfirmation.PermanentTitle, fst asked)

[<Fact>]
let ``a confirmed permanent delete is dispatched back with its mode`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with Prompts.Confirm = fun _ -> true }

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
            Processes.SendToTrash = fun path -> trashed.Add path; Ok() }

    Shell.interpret deps dispatch (DeletePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToTrash))

    Assert.Equal<FileSystemPath list>([ fp (at "start/notes.txt") ], List.ofSeq trashed)
    Assert.True(fs.File.Exists(at "start/notes.txt"))

    Assert.Equal<Message list>(
        [ DeleteFinished [ { Source = ItemOnDisk(fp (at "start/notes.txt")); Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``every entry sent to the trash is named while the batch runs`` () =
    let _, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    let reported = ResizeArray<FileSystemPath>()

    let deps =
        { Fakes.dependencies fs (at "start") with
            Runners.Batch = fun _ work finish -> work (recording reported) |> finish }

    Shell.interpret
        deps
        dispatch
        (DeletePlaces([ FromDisk [ fp (at "start/notes.txt"); fp (at "start/readme.txt") ] ], ToTrash))

    Assert.Equal<FileSystemPath list>(
        [ fp (at "start/notes.txt"); fp (at "start/readme.txt") ],
        List.ofSeq reported
    )

[<Fact>]
let ``a trash stopped with Esc leaves the entries it never reached`` () =
    let sent, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    let trashed = ResizeArray<FileSystemPath>()

    let deps =
        { Fakes.dependencies fs (at "start") with
            Processes.SendToTrash = fun path -> trashed.Add path; Ok()
            Runners.Batch = fun _ work finish -> work stopped |> finish }

    Shell.interpret
        deps
        dispatch
        (DeletePlaces([ FromDisk [ fp (at "start/notes.txt"); fp (at "start/readme.txt") ] ], ToTrash))

    Assert.Equal<FileSystemPath list>([ fp (at "start/notes.txt") ], List.ofSeq trashed)

    Assert.Equal<Message list>(
        [ DeleteFinished [ { Source = ItemOnDisk(fp (at "start/notes.txt")); Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``a permanent delete names every entry of the tree it removes`` () =
    let _, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    let reported = ResizeArray<FileSystemPath>()

    let deps =
        { Fakes.dependencies fs (at "start") with
            Runners.Batch = fun _ work finish -> work (recording reported) |> finish }

    Shell.interpret deps dispatch (DeletePlaces([ FromDisk [ fp (at "start/docs") ] ], Permanently))

    Assert.Equal<FileSystemPath list>(
        [ fp (at "start/docs"); fp (at "start/docs/inner.txt") ],
        List.ofSeq reported
    )

[<Fact>]
let ``a permanent delete stopped with Esc leaves the rest of the tree`` () =
    let _, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")

    let deps =
        { Fakes.dependencies fs (at "start") with
            Runners.Batch = fun _ work finish -> work stopped |> finish }

    Shell.interpret deps dispatch (DeletePlaces([ FromDisk [ fp (at "start/docs") ] ], Permanently))

    Assert.True(fs.File.Exists(at "start/docs/inner.txt"))

[<Fact>]
let ``a permanent delete removes the target itself`` () =
    let sent, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")

    let deps =
        { Fakes.dependencies fs (at "start") with
            Processes.SendToTrash = fun path -> failwith $"the trash was asked for {FileSystemPath.value path}" }

    Shell.interpret deps dispatch (DeletePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], Permanently))

    Assert.False(fs.File.Exists(at "start/notes.txt"))

    Assert.Equal<Message list>(
        [ DeleteFinished [ { Source = ItemOnDisk(fp (at "start/notes.txt")); Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``a wipe runs behind the progress window under a title of its own`` () =
    let sent, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    let titles = ResizeArray<string>()

    let deps =
        { Fakes.dependencies fs (at "start") with
            Processes.SendToTrash = fun path -> failwith $"the trash was asked for {FileSystemPath.value path}"
            Runners.Batch =
                fun title work finish ->
                    titles.Add title
                    work unattended |> finish }

    Shell.interpret deps dispatch (DeletePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ByWiping))

    Assert.Equal<string list>([ Shell.WipeTitle ], List.ofSeq titles)
    Assert.Empty(fs.Directory.GetFileSystemEntries(at "start"))

    Assert.Equal<Message list>(
        [ DeleteFinished [ { Source = ItemOnDisk(fp (at "start/notes.txt")); Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``a stopped wipe hands back the file it stopped at`` () =
    let sent, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")

    let deps =
        { Fakes.dependencies fs (at "start") with
            Runners.Batch = fun _ work finish -> work stopped |> finish }

    Shell.interpret
        deps
        dispatch
        (DeletePlaces([ FromDisk [ fp (at "start/notes.txt"); fp (at "start/readme.txt") ] ], ByWiping))

    Assert.True(fs.File.Exists(at "start/readme.txt"))

    Assert.Equal<Message list>(
        [ DeleteFinished
              [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
                  Outcome = Error(Cancelled(fp (at "start/notes.txt"))) } ] ],
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
            Processes.SendToTrash = fun _ -> Error refusal }

    Shell.interpret deps dispatch (DeletePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToTrash))

    Assert.Equal<Message list>(
        [ DeleteFinished [ { Source = ItemOnDisk(fp (at "start/notes.txt")); Outcome = Error refusal } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``item failures are reported under the operation title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    let failed =
        [ { Source = ItemOnDisk(fp (at "start/gone.txt"))
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

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToDirectory(fp (at "backup"))))

    Assert.Equal("new", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal<Message list>(
        [ CopyFinished [ { Source = ItemOnDisk(fp (at "start/notes.txt")); Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``a batch is handed to the runner under its own title`` () =
    let _, dispatch = capture ()
    let titles = ResizeArray<string>()
    let deps () =
        { noDeps () with
            FileSystem = copyReadyFs ()
            Runners.Batch = fun title work finish -> titles.Add title; work unattended |> finish }

    Shell.interpret
        (deps ())
        dispatch
        (CopyPlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToDirectory(fp (at "backup"))))
    Shell.interpret
        (deps ())
        dispatch
        (MovePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToDirectory(fp (at "backup"))))

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
            Prompts.AskOverwrite = (fun _ -> No)
            Runners.Batch = fun _ work finish -> started <- true; work unattended |> finish }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToDirectory(fp (at "backup"))))

    Assert.False started
    Assert.Empty sent
    Assert.Equal("old", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``a collision asks about the target that is already there`` () =
    let _, dispatch = capture ()
    let fs = copyReadyFs ()
    fs.AddFile(at "backup/notes.txt", MockFileData "old")
    let asked = ResizeArray<FileSystemPath>()

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.AskOverwrite = fun request -> asked.Add request.Target; No }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToDirectory(fp (at "backup"))))

    Assert.Equal(at "backup/notes.txt", FileSystemPath.value (Assert.Single asked))
    Assert.Equal("old", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``the target prompt suggests a path and carries back what was typed`` () =
    let sent, dispatch = capture ()
    let mutable offered = ("", "")
    let deps =
        { noDeps () with
            Prompts.AskTarget =
                fun request ->
                    offered <- (request.Title, FileSystemPath.value request.Suggestion)
                    Some "readme.txt" }

    Shell.interpret deps dispatch (PromptForTarget(fp (at "backup")))

    Assert.Equal(Shell.MovePrompt, fst offered)
    Assert.Equal(at "backup", snd offered)
    Assert.Contains(TargetEntered "readme.txt", sent)

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

    Shell.interpret
        deps
        dispatch
        (MovePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToDirectory(fp (at "backup"))))

    Assert.Equal("new", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.False(fs.File.Exists(at "start/notes.txt"))
    Assert.Equal<Message list>(
        [ MoveFinished [ { Source = ItemOnDisk(fp (at "start/notes.txt")); Outcome = Ok() } ] ],
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
let ``a rename the file system carries out itself asks for no batch`` () =
    let _, dispatch = capture ()
    let fs = copyReadyFs ()
    let titles = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = fs
            Runners.Batch = fun title work finish -> titles.Add title; work unattended |> finish }

    Shell.interpret deps dispatch (RenameTarget(fp (at "start/notes.txt"), en "readme.txt"))

    Assert.Empty titles

[<Fact>]
let ``a rename that fell back to copying runs behind the progress window`` () =
    let sent, dispatch = capture ()
    let fs = Fakes.DirectoryMoveRefused()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    let titles = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = fs
            Runners.Batch = fun title work finish -> titles.Add title; work unattended |> finish }

    Shell.interpret deps dispatch (RenameTarget(fp (at "start/docs"), en "papers"))

    Assert.Equal<string list>([ Shell.RenameTitle ], List.ofSeq titles)
    Assert.Equal("x", fs.File.ReadAllText(at "start/papers/inner.txt"))
    Assert.False(fs.Directory.Exists(at "start/docs"))
    Assert.Equal<Message list>([ RenameFinished(en "papers", Ok()) ], List.ofSeq sent)

[<Fact>]
let ``a rename stopped with Esc while it copied leaves the source where it is`` () =
    let sent, dispatch = capture ()
    let fs = Fakes.DirectoryMoveRefused()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")

    let deps =
        { noDeps () with
            FileSystem = fs
            Runners.Batch = fun _ work finish -> work stopped |> finish }

    Shell.interpret deps dispatch (RenameTarget(fp (at "start/docs"), en "papers"))

    Assert.True(fs.Directory.Exists(at "start/docs"))

    Assert.Equal<Message list>(
        [ RenameFinished(en "papers", Error(Cancelled(fp (at "start/docs")))) ],
        List.ofSeq sent
    )

[<Fact>]
let ``a non-empty directory among the targets widens the scope that comes back`` () =
    let sent, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret
        deps
        dispatch
        (InspectDelete({ Probed = [ fp (at "start/docs") ]; AtLeast = NamedEntriesOnly }, ToTrash))

    Assert.Equal<Message list>([ DeleteScopeLoaded(WithDirectoryContents, ToTrash) ], List.ofSeq sent)

[<Fact>]
let ``an empty directory keeps the scope that comes back to the named entries`` () =
    let sent, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret
        deps
        dispatch
        (InspectDelete({ Probed = [ fp (at "start/docs") ]; AtLeast = NamedEntriesOnly }, Permanently))

    Assert.Equal<Message list>([ DeleteScopeLoaded(NamedEntriesOnly, Permanently) ], List.ofSeq sent)

[<Fact>]
let ``a question that already reaches the contents asks nothing about its own paths`` () =
    let sent, dispatch = capture ()
    let fs = Fakes.WalkWitness()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret
        deps
        dispatch
        (InspectDelete({ Probed = [ fp (at "start/docs") ]; AtLeast = WithDirectoryContents }, Permanently))

    Assert.Equal(0, fs.AskedAbout(at "start/docs"))
    Assert.Equal<Message list>([ DeleteScopeLoaded(WithDirectoryContents, Permanently) ], List.ofSeq sent)

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

    let deps = { noDeps () with FileSystem = fs; Prompts.ShowFile = readOut }

    Shell.interpret deps ignore (ViewFile(fp (at "start/notes.txt")))

    Assert.Equal(at "start/notes.txt", fst shown)
    Assert.Equal<string list>([ "first"; "second" ], snd shown)

[<Fact>]
let ``a file that cannot be read is reported instead of shown`` () =
    let mutable shown = false
    let mutable reported = ("", "")

    let deps =
        { noDeps () with
            Prompts.ShowFile = fun _ -> shown <- true
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    Shell.interpret deps ignore (ViewFile(fp (at "start/gone.txt")))

    Assert.False shown
    Assert.Equal(ContentErrorMessage.Title, fst reported)
    Assert.Contains("gone.txt", snd reported)

[<Fact>]
let ``an edited file is handed to the editor and its outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let edited = ResizeArray<FileSystemPath>()
    let deps = { noDeps () with Processes.RunEditor = fun _ path -> edited.Add path; Ok() }

    Shell.interpret deps dispatch (EditFile(fp (at "start/notes.txt"), None))

    Assert.Equal(fp (at "start/notes.txt"), Assert.Single edited)
    Assert.Equal<Message list>([ EditFinished(Ok()) ], List.ofSeq sent)

[<Fact>]
let ``the editor the effect names is the one that is started`` () =
    let _, dispatch = capture ()
    let started = ResizeArray<ProgramPath option>()
    let deps = { noDeps () with Processes.RunEditor = fun chosen _ -> started.Add chosen; Ok() }
    let chosen = ProgramPath.tryCreate "code.exe"

    Shell.interpret deps dispatch (EditFile(fp (at "start/notes.txt"), chosen))

    Assert.Equal(chosen, Assert.Single started)

[<Fact>]
let ``an editor that fails dispatches its failure back`` () =
    let sent, dispatch = capture ()
    let failure = OperationFailed(fp (at "start/notes.txt"), "no editor")
    let deps = { noDeps () with Processes.RunEditor = fun _ _ -> Error failure }

    Shell.interpret deps dispatch (EditFile(fp (at "start/notes.txt"), None))

    Assert.Equal<Message list>([ EditFinished(Error failure) ], List.ofSeq sent)

[<Fact>]
let ``a name for a new file is dispatched as FileNameEntered`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with Prompts.AskName = fun _ -> Some "draft.md" }

    Shell.interpret deps dispatch PromptForFileName

    Assert.Contains(FileNameEntered(en "draft.md"), sent)

[<Fact>]
let ``a cancelled new file prompt dispatches nothing`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with Prompts.AskName = fun _ -> None }

    Shell.interpret deps dispatch PromptForFileName

    Assert.Empty sent

[<Fact>]
let ``the new file prompt opens on nothing`` () =
    let offered = ResizeArray<EnteredText option>()
    let deps = { noDeps () with Prompts.AskName = fun request -> offered.Add request.Suggestion; None }

    Shell.interpret deps ignore PromptForFileName

    Assert.Equal(None, Assert.Single offered)

[<Fact>]
let ``changed properties are dispatched back`` () =
    let sent, dispatch = capture ()
    let shown =
        { Path = fp (at "start/docs")
          Flags = Set.empty
          Created = DateTimeOffset(DateTime(2024, 1, 1))
          Modified = DateTimeOffset(DateTime(2024, 1, 1))
          Accessed = DateTimeOffset(DateTime(2024, 1, 1)) }
    let changed = EntryProperties.withFlag HiddenFlag shown
    let deps = { noDeps () with Prompts.EditProperties = fun _ -> Some changed }

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
let ``a program the effect names is handed to the launcher and its outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let asked = ResizeArray<LaunchRequest>()
    let deps = { noDeps () with Processes.Launch = fun request -> asked.Add request; Ok() }

    Shell.interpret deps dispatch (RunProgram(launching (at "start/notes.txt")))

    Assert.Equal(launching (at "start/notes.txt"), Assert.Single asked)
    Assert.Equal<Message list>([ LaunchFinished(launching (at "start/notes.txt"), Ok()) ], List.ofSeq sent)

[<Fact>]
let ``a launch that fails dispatches its failure back`` () =
    let sent, dispatch = capture ()
    let failure = NothingAssociated(fp (at "start/notes.odd"))
    let deps = { noDeps () with Processes.Launch = fun _ -> Error failure }

    Shell.interpret deps dispatch (RunProgram(launching (at "start/notes.odd")))

    Assert.Equal<Message list>([ LaunchFinished(launching (at "start/notes.odd"), Error failure) ], List.ofSeq sent)

[<Fact>]
let ``a launch failure is reported under its own title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    Shell.interpret deps ignore (ReportLaunchError(NothingAssociated(fp (at "start/notes.odd"))))

    Assert.Equal(LaunchErrorMessage.Title, fst reported)
    Assert.Contains("notes.odd", snd reported)

[<Fact>]
let ``the handlers of the file the effect names are the ones offered`` () =
    let asked = ResizeArray<EntryName * Handler list * ProgramPath option>()
    let firefox = Handler.tryCreate "Firefox" (ProgramPath.tryCreate "firefox.exe") |> Expect.some

    let deps =
        { noDeps () with
            Processes.HandlersFor = fun _ -> [ firefox ]
            Prompts.AskHandler = fun request -> asked.Add(request.Name, request.Handlers, request.Chosen); None }

    Shell.interpret deps ignore (ShowHandlers(fp (at "start/page.html"), ProgramPath.tryCreate "code.exe"))

    let name, handlers, chosen = Assert.Single asked

    Assert.Equal(en "page.html", name)
    Assert.Equal(firefox, Assert.Single handlers)
    Assert.Equal(ProgramPath.tryCreate "code.exe", chosen)

[<Fact>]
let ``a program picked out of the popup is dispatched back with the file it opens`` () =
    let sent, dispatch = capture ()

    let choice =
        { Program = Expect.some (ProgramPath.tryCreate "firefox.exe")
          Use = AlwaysThisWay }

    let deps = { noDeps () with Prompts.AskHandler = fun _ -> Some choice }

    Shell.interpret deps dispatch (ShowHandlers(fp (at "start/page.html"), None))

    Assert.Contains(HandlerChosen(fp (at "start/page.html"), choice), sent)

[<Fact>]
let ``a dismissed open-with popup dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (ShowHandlers(fp (at "start/page.html"), None))

    Assert.Empty sent

[<Fact>]
let ``changed programs are dispatched back`` () =
    let sent, dispatch = capture ()
    let changed =
        { ProgramSettings.none with
            DefaultEditor = ProgramPath.tryCreate "code.exe" }
    let deps = { noDeps () with Prompts.EditPrograms = fun _ -> Some changed }

    Shell.interpret deps dispatch (ShowSettings(ProgramSettings.none, None))

    Assert.Equal<Message list>([ ProgramsEntered changed ], List.ofSeq sent)

[<Fact>]
let ``the executable the effect names is offered to the settings popup`` () =
    let offered = ResizeArray<ProgramPath option>()
    let deps = { noDeps () with Prompts.EditPrograms = fun request -> offered.Add request.Candidate; None }
    let candidate = ProgramPath.tryCreate (at "start/rider64.exe")

    Shell.interpret deps ignore (ShowSettings(ProgramSettings.none, candidate))

    Assert.Equal(candidate, Assert.Single offered)

[<Fact>]
let ``a dismissed settings dialog dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (ShowSettings(ProgramSettings.none, None))

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
let ``a session that could not be written is reported`` () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")
    fs.AddFile(at "blocked", MockFileData "x")
    let mutable reported = ("", "")

    let deps =
        { noDeps () with
            FileSystem = fs
            SettingsPath = fp (at "blocked/marbas.conf")
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    Shell.interpret deps ignore (SaveSettings(Settings.forPath (fp (at "start"))))

    Assert.Equal(SettingsErrorMessage.Title, fst reported)
    Assert.Contains(at "blocked/marbas.conf", snd reported)

[<Fact>]
let ``a history change is written where the history path points, stamped by the clock`` () =
    let fs = MockFileSystem()
    let sent, dispatch = capture ()
    let now = DateTimeOffset(2026, 9, 28, 15, 30, 0, TimeSpan.FromHours 2.0)
    let change = Remember(Visited(Place.OnDisk(fp (at "work"))))

    let deps =
        { noDeps () with
            FileSystem = fs
            HistoryPath = fp (at "config/history.conf")
            Clock = fun () -> now }

    Shell.interpret deps dispatch (ChangeHistory change)

    let written = History.apply Windows now change History.empty
    Assert.Equal(Ok written, History.load fs (fp (at "config/history.conf")))
    Assert.Equal<Message list>([ HistoryChanged written ], List.ofSeq sent)

[<Fact>]
let ``a history change that could not be written is dispatched as the error`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "blocked", MockFileData "x")
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            FileSystem = fs
            HistoryPath = fp (at "blocked/history.conf") }

    Shell.interpret deps dispatch (ChangeHistory(Remember(Visited(Place.OnDisk(fp (at "work"))))))

    Assert.Equal<FileSystemPath option list>(
        [ Some(fp (at "blocked/history.conf")) ],
        List.ofSeq sent |> List.map unwrittenHistory
    )

[<Fact>]
let ``a history that could not be written is reported with its path`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    Shell.interpret deps ignore (ReportHistoryError(HistoryAccessDenied(fp (at "config/history.conf"))))

    Assert.Equal(HistoryErrorMessage.Title, fst reported)
    Assert.Contains(at "config/history.conf", snd reported)

[<Fact>]
let ``the history is read afresh and offered on the tab asked for`` () =
    let fs = MockFileSystem()
    let now = DateTimeOffset(2026, 9, 28, 15, 30, 0, TimeSpan.FromHours 2.0)
    let path = fp (at "config/history.conf")
    let stored = History.change fs path now (Remember(Visited(Place.OnDisk(fp (at "work"))))) |> Expect.ok
    let asked = ResizeArray<Ports.HistoryPrompt>()
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            FileSystem = fs
            HistoryPath = path
            Clock = fun () -> now
            Prompts.ChooseHistory =
                fun prompt ->
                    asked.Add prompt
                    { Tab = prompt.Tab; Choice = None } }

    Shell.interpret deps dispatch (ShowHistory ViewHistoryTab)

    Assert.Equal<(History * HistoryTab * DateTimeOffset) list>(
        [ stored, ViewHistoryTab, now ],
        asked |> Seq.map (fun prompt -> prompt.History, prompt.Tab, prompt.Now) |> List.ofSeq
    )
    Assert.Equal<Message list>(
        [ HistoryChanged stored; HistoryAnswered { Tab = ViewHistoryTab; Choice = None } ],
        List.ofSeq sent
    )

let private historyAt = fp (at "config/history.conf")

let private visited path = Visited(Place.OnDisk(fp (at path)))

let private storing (fs: MockFileSystem) items =
    items
    |> List.iter (fun item -> History.change fs historyAt DateTimeOffset.UnixEpoch (Remember item) |> ignore)

let private changingFromPopup fs path change (answers: ResizeArray<History option>) =
    { noDeps () with
        FileSystem = fs
        HistoryPath = path
        Clock = fun () -> DateTimeOffset(2026, 9, 28, 15, 30, 0, TimeSpan.FromHours 2.0)
        Prompts.ChooseHistory =
            fun prompt ->
                answers.Add(prompt.Change change)
                { Tab = prompt.Tab; Choice = None } }

[<Fact>]
let ``an entry forgotten in the popup is gone from the file and the others stay`` () =
    let fs = MockFileSystem()
    storing fs [ visited "older"; visited "newer" ]
    let deps = changingFromPopup fs historyAt (Forget(visited "older")) (ResizeArray())

    Shell.interpret deps ignore (ShowHistory FolderHistoryTab)

    let read = History.load fs historyAt |> Expect.ok
    Assert.Equal<Place list>([ Place.OnDisk(fp (at "newer")) ], read.Folders |> List.map _.Item)

[<Fact>]
let ``a list cleared in the popup is still empty when the file is read again`` () =
    let fs = MockFileSystem()
    let seen = { Where = Place.OnDisk(fp (at "notes.md")); How = Viewed }
    storing fs [ visited "older"; visited "newer"; Opened seen ]
    let deps = changingFromPopup fs historyAt (ClearList FolderHistory) (ResizeArray())

    Shell.interpret deps ignore (ShowHistory FolderHistoryTab)

    let read = History.load fs historyAt |> Expect.ok
    Assert.Empty read.Folders
    Assert.Equal<FileVisit list>([ seen ], read.FileVisits |> List.map _.Item)

[<Fact>]
let ``a change made in the popup answers it with what the file now holds`` () =
    let fs = MockFileSystem()
    storing fs [ visited "older"; visited "newer" ]
    let answers = ResizeArray<History option>()
    let deps = changingFromPopup fs historyAt (Forget(visited "older")) answers

    Shell.interpret deps ignore (ShowHistory FolderHistoryTab)

    Assert.Equal<History option list>([ Some(History.load fs historyAt |> Expect.ok) ], List.ofSeq answers)

[<Fact>]
let ``a change made in the popup reaches the state as well`` () =
    let fs = MockFileSystem()
    storing fs [ visited "older"; visited "newer" ]
    let sent, dispatch = capture ()
    let deps = changingFromPopup fs historyAt (Forget(visited "older")) (ResizeArray())

    Shell.interpret deps dispatch (ShowHistory FolderHistoryTab)

    Assert.Contains(HistoryChanged(History.load fs historyAt |> Expect.ok), sent)

[<Fact>]
let ``a change the file refused answers the popup nothing and reaches the state as the error`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "blocked", MockFileData "x")
    let answers = ResizeArray<History option>()
    let sent, dispatch = capture ()
    let deps = changingFromPopup fs (fp (at "blocked/history.conf")) (ClearList FolderHistory) answers

    Shell.interpret deps dispatch (ShowHistory FolderHistoryTab)

    Assert.Equal<History option list>([ None ], List.ofSeq answers)
    Assert.Contains(Some(fp (at "blocked/history.conf")), sent |> Seq.map unwrittenHistory)

[<Fact>]
let ``a history that cannot be read is reported instead of offered`` () =
    let path = fp (at "config/history.conf")
    let fs = Fakes.UnreadableFile(FileSystemPath.value path)
    fs.AddFile(FileSystemPath.value path, MockFileData "folder.1=x")
    let mutable reported = ""
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            FileSystem = fs
            HistoryPath = path
            Prompts.ReportError = fun report -> reported <- report.Title }

    Shell.interpret deps dispatch (ShowHistory FolderHistoryTab)

    Assert.Equal(HistoryErrorMessage.Title, reported)
    Assert.Empty sent

[<Fact>]
let ``quit runs the quit port`` () =
    let mutable quit = false
    let deps = { noDeps () with OnQuit = fun () -> quit <- true }

    Shell.interpret deps ignore Quit

    Assert.True quit

[<Fact>]
let ``quit takes the workspace of the session with it`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "temp/Marbas/session/left.txt", MockFileData "left")

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session") }

    Shell.interpret deps ignore Quit

    Assert.False(fs.Directory.Exists(at "temp/Marbas/session"))

let private typed raw = EnteredText.tryCreate raw |> Expect.some

let private rememberedIn (fs: MockFileSystem) kind =
    History.load fs (fp (at "history.conf"))
    |> Expect.ok
    |> History.inputs kind
    |> List.map (_.Item >> EnteredText.value)

let private typedEarlier (fs: MockFileSystem) kind texts =
    texts
    |> List.iter (fun text ->
        History.change fs (fp (at "history.conf")) DateTimeOffset.UnixEpoch (Remember(Typed(kind, typed text)))
        |> ignore)

let private withFs fs = Fakes.dependencies fs (at "start")

[<Fact>]
let ``the name prompt opens on the name the last directory was given`` () =
    let fs = MockFileSystem()
    typedEarlier fs NewDirectory [ "older"; "work" ]
    let mutable offered = None
    let deps = { withFs fs with Prompts.AskName = fun request -> offered <- request.Suggestion; None }

    Shell.interpret deps ignore PromptForName

    Assert.Equal(Some(typed "work"), offered)

[<Fact>]
let ``the name prompt starts empty when nothing was remembered`` () =
    let mutable offered = Some(typed "something")
    let deps = { noDeps () with Prompts.AskName = fun request -> offered <- request.Suggestion; None }

    Shell.interpret deps ignore PromptForName

    Assert.Equal(None, offered)

[<Fact>]
let ``the name prompt offers the names directories were given, newest first`` () =
    let fs = MockFileSystem()
    typedEarlier fs NewDirectory [ "older"; "work" ]
    let offered = ResizeArray<EnteredText list>()
    let deps = { withFs fs with Prompts.AskName = fun request -> offered.Add request.Remembered; None }

    Shell.interpret deps ignore PromptForName

    Assert.Equal<EnteredText list>([ typed "work"; typed "older" ], Assert.Single offered)

[<Fact>]
let ``a name given to a directory is left for the state to remember`` () =
    let fs = MockFileSystem()
    let deps = { withFs fs with Prompts.AskName = fun _ -> Some "work" }

    Shell.interpret deps ignore PromptForName

    Assert.Empty(rememberedIn fs NewDirectory)

[<Fact>]
let ``the target prompt offers the targets typed before, newest first`` () =
    let fs = MockFileSystem()
    typedEarlier fs CopyOrMoveTarget [ @"D:\older"; @"D:\newer" ]
    let offered = ResizeArray<EnteredText list>()
    let deps = { withFs fs with Prompts.AskTarget = fun request -> offered.Add request.Remembered; None }

    Shell.interpret deps ignore (PromptForTarget(fp (at "backup")))

    Assert.Equal<EnteredText list>([ typed @"D:\newer"; typed @"D:\older" ], Assert.Single offered)

[<Fact>]
let ``a target confirmed is remembered as it was typed`` () =
    let fs = MockFileSystem()
    let deps = { withFs fs with Prompts.AskTarget = fun _ -> Some @" D:\backup " }

    Shell.interpret deps ignore (PromptForTarget(fp (at "backup")))

    Assert.Equal<string list>([ @" D:\backup " ], rememberedIn fs CopyOrMoveTarget)

[<Fact>]
let ``a target prompt cancelled remembers nothing`` () =
    let fs = MockFileSystem()

    Shell.interpret (withFs fs) ignore (PromptForTarget(fp (at "backup")))

    Assert.False(fs.File.Exists(at "history.conf"))

[<Fact>]
let ``a blank target confirmed remembers nothing`` () =
    let fs = MockFileSystem()
    let deps = { withFs fs with Prompts.AskTarget = fun _ -> Some "   " }

    Shell.interpret deps ignore (PromptForTarget(fp (at "backup")))

    Assert.False(fs.File.Exists(at "history.conf"))

[<Fact>]
let ``the new file prompt offers the names typed before`` () =
    let fs = MockFileSystem()
    typedEarlier fs NewFile [ "notes.md" ]
    let offered = ResizeArray<EnteredText list>()
    let deps = { withFs fs with Prompts.AskName = fun request -> offered.Add request.Remembered; None }

    Shell.interpret deps ignore PromptForFileName

    Assert.Equal<EnteredText list>([ typed "notes.md" ], Assert.Single offered)

[<Fact>]
let ``a name for a new file confirmed is remembered`` () =
    let fs = MockFileSystem()
    let deps = { withFs fs with Prompts.AskName = fun _ -> Some "draft.md" }

    Shell.interpret deps ignore PromptForFileName

    Assert.Equal<string list>([ "draft.md" ], rememberedIn fs NewFile)

[<Fact>]
let ``a new file prompt cancelled remembers nothing`` () =
    let fs = MockFileSystem()

    Shell.interpret (withFs fs) ignore PromptForFileName

    Assert.False(fs.File.Exists(at "history.conf"))

[<Fact>]
let ``the pattern prompts offer one list between selecting and deselecting`` () =
    let fs = MockFileSystem()
    let offered = ResizeArray<EnteredText list>()
    let deps = { withFs fs with Prompts.AskPattern = fun request -> offered.Add request.Remembered; Some "*.fs" }
    Shell.interpret deps ignore (PromptForPattern Select)

    Shell.interpret deps ignore (PromptForPattern Deselect)

    Assert.Equal<EnteredText list>([ typed "*.fs" ], offered[1])

[<Fact>]
let ``a pattern confirmed is remembered`` () =
    let fs = MockFileSystem()
    let deps = { withFs fs with Prompts.AskPattern = fun _ -> Some "*.txt" }

    Shell.interpret deps ignore (PromptForPattern Deselect)

    Assert.Equal<string list>([ "*.txt" ], rememberedIn fs SelectionPattern)

[<Fact>]
let ``a pattern prompt cancelled remembers nothing`` () =
    let fs = MockFileSystem()

    Shell.interpret (withFs fs) ignore (PromptForPattern Select)

    Assert.False(fs.File.Exists(at "history.conf"))

[<Fact>]
let ``a blank pattern is still applied but not remembered`` () =
    let fs = MockFileSystem()
    let sent, dispatch = capture ()
    let deps = { withFs fs with Prompts.AskPattern = fun _ -> Some "  " }

    Shell.interpret deps dispatch (PromptForPattern Select)

    Assert.Contains(PatternEntered(Select, NamePattern.tryCreate "  " |> Expect.some), sent)
    Assert.False(fs.File.Exists(at "history.conf"))

let private packReadyFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "new")
    fs.AddDirectory(at "backup")
    fs

let private zipFormat = ArchiveFormat.tryCreate Zip |> Expect.some

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
            Prompts.AskArchive =
                fun request ->
                    offered <- FileSystemPath.value request.Suggestion
                    Some choice }

    Shell.interpret deps dispatch (PromptForArchive(fp (at "backup/notes.zip")))

    Assert.Equal(at "backup/notes.zip", offered)
    Assert.Contains(ArchiveEntered choice, sent)

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
        [ PackFinished(en "notes.zip", [ { Source = ItemOnDisk(fp (at "start/notes.txt")); Outcome = Ok() } ]) ],
        List.ofSeq sent
    )

[<Fact>]
let ``packing runs under its own title`` () =
    let _, dispatch = capture ()
    let titles = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = packReadyFs ()
            Runners.Batch =
                fun title work finish ->
                    titles.Add title
                    work unattended |> finish }

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
            Prompts.Confirm =
                fun question ->
                    asked <- (question.Title, question.Question)
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
    let deps = { noDeps () with FileSystem = fs; Prompts.Confirm = fun _ -> false }

    Shell.interpret deps dispatch (ArchiveTargets([ fp (at "start/notes.txt") ], packing (at "backup/notes.zip")))

    Assert.Empty sent

[<Fact>]
let ``an accepted overwrite replaces the archive`` () =
    let sent, dispatch = capture ()
    let fs = packReadyFs ()
    fs.AddFile(at "backup/notes.zip", MockFileData "old")
    let deps = { noDeps () with FileSystem = fs; Prompts.Confirm = fun _ -> true }

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
              [ { Source = ItemInArchive(dataZip, entry "notes.txt"); Outcome = Ok() }
                { Source = ItemInArchive(dataZip, entry "docs"); Outcome = Ok() } ]
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
            Runners.Batch =
                fun title work finish ->
                    titles.Add title
                    work unattended |> finish }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.zip") ], fp (at "backup")))

    Assert.Equal<string list>([ Shell.UnpackTitle ], List.ofSeq titles)

[<Fact>]
let ``an entry already in the target directory is asked about`` () =
    let _, dispatch = capture ()
    let fs = unpackReadyFs None
    fs.AddFile(at "backup/notes.txt", MockFileData "old")
    let asked = ResizeArray<FileSystemPath>()

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.AskOverwrite =
                fun request ->
                    asked.Add request.Target
                    No }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.zip") ], fp (at "backup")))

    Assert.Equal(1, asked.Count)
    Assert.Equal(at "backup/notes.txt", FileSystemPath.value asked[0])
    Assert.Equal("old", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal("inner", fs.File.ReadAllText(at "backup/docs/inner.txt"))

[<Fact>]
let ``a protected archive is asked for its password once`` () =
    let fs = unpackReadyFs (ArchivePassword.tryCreate "letmein")
    let _, dispatch = capture ()
    let asked = ResizeArray<EntryName>()

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.AskPassword =
                fun request ->
                    asked.Add request.Archive
                    ArchivePassword.tryCreate "letmein" }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.zip") ], fp (at "backup")))

    Assert.Equal(1, asked.Count)
    Assert.Equal(en "data.zip", asked[0])
    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``an archive that needs no password is never asked for one`` () =
    let _, dispatch = capture ()
    let mutable asked = false

    let deps =
        { noDeps () with
            FileSystem = unpackReadyFs None
            Prompts.AskPassword =
                fun _ ->
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
    fs.AddFile(at "start/data.bin", MockFileData "whatever")
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (ExtractTargets([ fp (at "start/data.bin") ], fp (at "backup")))

    Assert.Equal<Message list>(
        [ UnpackFinished(
              [ { Source = ItemOnDisk(fp (at "start/data.bin"))
                  Outcome = Error(ArchiveFailed(KindCannotBeRead(fp (at "start/data.bin")))) } ]
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
            Prompts.AskPassword = fun _ -> None }

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
        unattended
        { Path = fp (at "backup/data.zip")
          Format = ArchiveFormat.tryCreate Zip |> Expect.some
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

    let deps = { noDeps () with FileSystem = fs; Prompts.ShowFile = readOut }
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
            Prompts.ShowFile = fun _ -> shown <- true
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

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
        unattended
        { Path = fp (at "backup/locked.zip")
          Format = ArchiveFormat.tryCreate Zip |> Expect.some
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
            Prompts.AskPassword = fun _ -> asked <- asked + 1; ArchivePassword.tryCreate "opensesame"
            Prompts.ShowFile = fun source -> shown <- Text.Encoding.ASCII.GetString(source.Read(0L, 6)) }

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
        unattended
        { Path = fp (at "backup/locked.zip")
          Format = ArchiveFormat.tryCreate Zip |> Expect.some
          Compression = Normal
          Password = ArchivePassword.tryCreate "opensesame" }
        [ fp (at "work/notes.txt") ]
    |> ignore

    let mutable shown = false
    let entry = ArchivePath.tryCreate "notes.txt" |> Expect.some

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.AskPassword = fun _ -> None
            Prompts.ShowFile = fun _ -> shown <- true }

    Shell.interpret deps ignore (ViewArchiveEntry(fp (at "backup/locked.zip"), entry))

[<Fact>]
let ``entries copied out of an archive land in the target directory and the outcome is dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs None
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "notes.txt"; entry "docs" ]) ], ToDirectory(fp (at "backup"))))

    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal("inner", fs.File.ReadAllText(at "backup/docs/inner.txt"))

    Assert.Equal<Message list>(
        [ CopyFinished(
              [ { Source = ItemInArchive(dataZip, entry "notes.txt"); Outcome = Ok() }
                { Source = ItemInArchive(dataZip, entry "docs"); Outcome = Ok() } ]
          ) ],
        List.ofSeq sent
    )

[<Fact>]
let ``an entry deeper in an archive is copied out under its own name`` () =
    let _, dispatch = capture ()
    let fs = unpackReadyFs None
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "docs/inner.txt" ]) ], ToDirectory(fp (at "backup"))))

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
            Runners.Batch =
                fun title work finish ->
                    titles.Add title
                    work unattended |> finish }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "notes.txt" ]) ], ToDirectory(fp (at "backup"))))

    Assert.Equal<string list>([ Shell.CopyTitle ], List.ofSeq titles)
    Assert.Equal<byte list>(List.ofArray before, List.ofArray (fs.File.ReadAllBytes(at "start/data.zip")))

[<Fact>]
let ``an entry copied over one already there is asked about by its landing place`` () =
    let _, dispatch = capture ()
    let fs = unpackReadyFs None
    fs.AddFile(at "backup/notes.txt", MockFileData "old")
    let asked = ResizeArray<FileSystemPath>()

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.AskOverwrite =
                fun request ->
                    asked.Add request.Target
                    No }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "notes.txt" ]) ], ToDirectory(fp (at "backup"))))

    Assert.Equal(at "backup/notes.txt", FileSystemPath.value (Assert.Single asked))
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
            Prompts.AskOverwrite = fun _ -> No
            Runners.Batch =
                fun title work finish ->
                    batches.Add title
                    work unattended |> finish }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "notes.txt" ]) ], ToDirectory(fp (at "backup"))))

    Assert.Empty batches
    Assert.Empty sent

[<Fact>]
let ``copying out of a locked archive asks for the password once`` () =
    let _, dispatch = capture ()
    let fs = unpackReadyFs (ArchivePassword.tryCreate "opensesame")
    let asked = ResizeArray<EntryName>()

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.AskPassword =
                fun request ->
                    asked.Add request.Archive
                    ArchivePassword.tryCreate "opensesame" }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "notes.txt" ]) ], ToDirectory(fp (at "backup"))))

    Assert.Single asked |> ignore
    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))

[<Fact>]
let ``copying out of a locked archive whose password is not given copies nothing`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs (ArchivePassword.tryCreate "opensesame")
    let deps = { noDeps () with FileSystem = fs; Prompts.AskPassword = fun _ -> None }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "notes.txt" ]) ], ToDirectory(fp (at "backup"))))

    Assert.False(fs.File.Exists(at "backup/notes.txt"))
    Assert.Empty sent

let private zipAt = dataZip

let private withLooseFile () =
    let fs = unpackReadyFs None
    fs.AddFile(at "backup/fresh.txt", MockFileData "fresh")
    fs

let private levelNames (fs: MockFileSystem) level =
    ArchiveListing.read fs zipAt level
    |> Expect.ok
    |> List.map (_.Name >> EntryName.value)
    |> List.sort

[<Fact>]
let ``sources added to an archive are in it and the outcome is dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = withLooseFile ()
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromDisk [ fp (at "backup/fresh.txt") ] ], ToArchive(zipAt, ArchivePath.root)))

    Assert.Equal<string list>([ "docs"; "fresh.txt"; "notes.txt" ], levelNames fs ArchivePath.root)
    Assert.Equal<Message list>(
        [ CopyFinished [ { Source = ItemOnDisk(fp (at "backup/fresh.txt")); Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``a name already at that level of the archive is asked about`` () =
    let _, dispatch = capture ()
    let fs = withLooseFile ()
    fs.AddFile(at "backup/notes.txt", MockFileData "fresher")
    let asked = ResizeArray<FileSystemPath>()

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.AskOverwrite =
                fun request ->
                    asked.Add request.Target
                    No }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromDisk [ fp (at "backup/notes.txt") ] ], ToArchive(zipAt, ArchivePath.root)))

    Assert.Equal(at "start/data.zip/notes.txt", FileSystemPath.value (Assert.Single asked))

[<Fact>]
let ``a kind that cannot be changed comes back as a failed item`` () =
    let sent, dispatch = capture ()
    let fs = withLooseFile ()
    fs.AddFile(at "start/data.rar", MockFileData "whatever")
    let rarAt = fp (at "start/data.rar")
    let mutable reported = ("", "")

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message)
            Runners.Batch = fun _ work finish -> work unattended |> finish }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromDisk [ fp (at "backup/fresh.txt") ] ], ToArchive(rarAt, ArchivePath.root)))

    Assert.Equal<Message list>(
        [ CopyFinished
              [ { Source = ItemOnDisk rarAt
                  Outcome = Error(ArchiveFailed(KindCannotBeChanged rarAt)) } ] ],
        List.ofSeq sent
    )

    Assert.Equal(("", ""), reported)

[<Fact>]
let ``an archive behind a password comes back as a failed item`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs (ArchivePassword.tryCreate "opensesame")
    fs.AddFile(at "backup/fresh.txt", MockFileData "fresh")
    let mutable reported = ("", "")

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message)
            Runners.Batch = fun _ work finish -> work unattended |> finish }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromDisk [ fp (at "backup/fresh.txt") ] ], ToArchive(zipAt, ArchivePath.root)))

    Assert.Equal<Message list>(
        [ CopyFinished
              [ { Source = ItemOnDisk zipAt
                  Outcome = Error(ArchiveFailed(ProtectedArchiveCannotBeChanged zipAt)) } ] ],
        List.ofSeq sent
    )

    Assert.Equal(("", ""), reported)

[<Fact>]
let ``a source moved into an archive is gone from disk once it is in`` () =
    let sent, dispatch = capture ()
    let fs = withLooseFile ()
    let titles = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = fs
            Runners.Batch =
                fun title work finish ->
                    titles.Add title
                    work unattended |> finish }

    Shell.interpret
        deps
        dispatch
        (MovePlaces([ FromDisk [ fp (at "backup/fresh.txt") ] ], ToArchive(zipAt, ArchivePath.root)))

    Assert.Equal<string list>([ "docs"; "fresh.txt"; "notes.txt" ], levelNames fs ArchivePath.root)
    Assert.False(fs.File.Exists(at "backup/fresh.txt"))
    Assert.Equal<string list>([ Shell.MoveTitle ], List.ofSeq titles)
    Assert.Equal<Message list>(
        [ MoveFinished [ { Source = ItemOnDisk(fp (at "backup/fresh.txt")); Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``an entry moved out of an archive is on disk and gone from the archive`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs None
    let entry = ArchivePath.tryCreate "notes.txt" |> Expect.some

    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (MovePlaces([ FromArchive(zipAt, [ entry ]) ], ToDirectory(fp (at "backup"))))

    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal<string list>([ "docs" ], levelNames fs ArchivePath.root)
    Assert.Equal<Message list>(
        [ MoveFinished [ { Source = ItemInArchive(zipAt, entry); Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``entries removed from an archive are gone and the outcome is dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs None
    let entry = ArchivePath.tryCreate "notes.txt" |> Expect.some
    let titles = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = fs
            Runners.Batch =
                fun title work finish ->
                    titles.Add title
                    work unattended |> finish }

    Shell.interpret deps dispatch (DeletePlaces([ FromArchive(zipAt, [ entry ]) ], Permanently))

    Assert.Equal<string list>([ "docs" ], levelNames fs ArchivePath.root)
    Assert.Equal<string list>([ Shell.DeleteTitle ], List.ofSeq titles)
    Assert.Equal<Message list>(
        [ DeleteFinished [ { Source = ItemInArchive(zipAt, entry); Outcome = Ok() } ] ],
        List.ofSeq sent
    )

let private otherAt = fp (at "start/other.zip")

let private twoArchives () =
    let fs = unpackReadyFs None

    Archive.write
        fs
        unattended
        { Path = otherAt
          Format = zipFormat
          Compression = Normal
          Password = None }
        [ fp (at "start/work/notes.txt") ]
    |> ignore

    fs

let private namesIn (fs: MockFileSystem) archive level =
    ArchiveListing.read fs archive level
    |> Expect.ok
    |> List.map (_.Name >> EntryName.value)
    |> List.sort

[<Fact>]
let ``an entry copied across archives lands in the other and stays in its own`` () =
    let sent, dispatch = capture ()
    let fs = twoArchives ()

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session") }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(zipAt, [ entry "docs" ]) ], ToArchive(otherAt, ArchivePath.root)))

    Assert.Equal<string list>([ "docs"; "notes.txt" ], namesIn fs otherAt ArchivePath.root)
    Assert.Equal<string list>([ "inner.txt" ], namesIn fs otherAt (entry "docs"))
    Assert.Equal<string list>([ "docs"; "notes.txt" ], namesIn fs zipAt ArchivePath.root)
    Assert.Equal<Message list>(
        [ CopyFinished [ { Source = ItemInArchive(zipAt, entry "docs"); Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``an entry moved across archives leaves the one it came from`` () =
    let _, dispatch = capture ()
    let fs = twoArchives ()

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session") }

    Shell.interpret
        deps
        dispatch
        (MovePlaces([ FromArchive(zipAt, [ entry "docs" ]) ], ToArchive(otherAt, ArchivePath.root)))

    Assert.Equal<string list>([ "docs"; "notes.txt" ], namesIn fs otherAt ArchivePath.root)
    Assert.Equal<string list>([ "notes.txt" ], namesIn fs zipAt ArchivePath.root)

[<Fact>]
let ``a name already at the target level of the other archive is asked about`` () =
    let _, dispatch = capture ()
    let fs = twoArchives ()
    let asked = ResizeArray<FileSystemPath>()

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session")
            Prompts.AskOverwrite =
                fun request ->
                    asked.Add request.Target
                    No }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(zipAt, [ entry "notes.txt" ]) ], ToArchive(otherAt, ArchivePath.root)))

    Assert.Equal(at "start/other.zip/notes.txt", FileSystemPath.value (Assert.Single asked))

[<Fact>]
let ``the workspace is empty once a crossing has finished`` () =
    let _, dispatch = capture ()
    let fs = twoArchives ()

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session") }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(zipAt, [ entry "docs" ]) ], ToArchive(otherAt, ArchivePath.root)))

    Assert.Empty(fs.Directory.EnumerateFileSystemEntries(at "temp/Marbas/session"))

[<Fact>]
let ``a crossing out of a locked archive asks for its password once`` () =
    let _, dispatch = capture ()
    let fs = unpackReadyFs (ArchivePassword.tryCreate "opensesame")

    Archive.write
        fs
        unattended
        { Path = otherAt
          Format = zipFormat
          Compression = Normal
          Password = None }
        [ fp (at "start/work/notes.txt") ]
    |> ignore

    let asked = ResizeArray<EntryName>()

    let deps =
        { noDeps () with
            FileSystem = fs
            WorkspacePath = fp (at "temp/Marbas/session")
            Prompts.AskPassword =
                fun request ->
                    asked.Add request.Archive
                    ArchivePassword.tryCreate "opensesame" }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(zipAt, [ entry "docs" ]) ], ToArchive(otherAt, ArchivePath.root)))

    Assert.Single asked |> ignore
    Assert.Equal<string list>([ "docs"; "notes.txt" ], namesIn fs otherAt ArchivePath.root)

[<Fact>]
let ``the paths under watch are handed to the watch`` () =
    let _, dispatch = capture ()
    let mutable watched = []

    let deps =
        { noDeps () with
            Watches.Locations = fun paths _ -> watched <- paths }

    Shell.interpret deps dispatch (WatchLocations [ fp (at "start"); fp (at "backup") ])

    Assert.Equal<FileSystemPath list>([ fp (at "start"); fp (at "backup") ], watched)

[<Fact>]
let ``what the watch reports is dispatched as an external change`` () =
    let sent, dispatch = capture ()
    let mutable report = ignore

    let deps =
        { noDeps () with
            Watches.Locations = fun _ reported -> report <- reported }

    Shell.interpret deps dispatch (WatchLocations [ fp (at "start") ])
    report ()

    Assert.Equal<Message list>([ ExternalChangeDetected ], List.ofSeq sent)

let private searchFor scope =
    { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
      Scope = scope
      Hidden = Concealed
      Links = StopAtLinks
      Archives = SkipArchives }

let private answering landing =
    { Landing = landing
      Results =
        { Query = searchFor FromDirectory
          Ground = { Roots = []; OtherRoots = [] }
          Hits = []
          Outcome = FileSearch.Finished { Files = 0; Directories = 0; Skipped = 0 }
          Cursor = 0 } }

[<Fact>]
let ``the search runs over the ground its scope names`` () =
    let mutable asked = { Roots = []; OtherRoots = [] }

    let deps =
        { noDeps () with
            Runners.Search = fun run _ _ -> asked <- SearchRun.ground run }

    Shell.interpret deps ignore (RunSearch(searchFor FromDirectory, OnDisk(fp (at "start")), []))

    Assert.Equal<PanelLocation list>([ OnDisk(fp (at "start")) ], asked.Roots)

[<Fact>]
let ``the directories of PATH are the ground of the scope that names them`` () =
    let mutable asked = { Roots = []; OtherRoots = [] }

    let deps =
        { noDeps () with
            Processes.PathDirectories = [ fp (at "tools") ]
            Runners.Search = fun run _ _ -> asked <- SearchRun.ground run }

    Shell.interpret deps ignore (RunSearch(searchFor PathDirectories, OnDisk(fp (at "start")), []))

    Assert.Equal<PanelLocation list>([ OnDisk(fp (at "tools")) ], asked.Roots)

[<Fact>]
let ``what the results window answered becomes a message`` () =
    let sent, dispatch = capture ()
    let landing = AtDirectory(OnDisk(fp (at "start/docs")))

    let deps =
        { noDeps () with
            Runners.Search = fun _ _ finished -> finished (answering (Some landing)) }

    Shell.interpret deps dispatch (RunSearch(searchFor FromDirectory, OnDisk(fp (at "start")), []))

    Assert.Equal<Message list>([ SearchAnswered(answering (Some landing)) ], List.ofSeq sent)

[<Fact>]
let ``a hit the results window asks to view is read out of the file it names`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "first")
    let mutable shown = ""

    let hit: FindHit =
        { Location = OnDisk(fp (at "start"))
          Entry = DirectoryListing.entryFromInfo (fs.FileInfo.New(at "start/notes.txt")) }

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.ShowFile = fun source -> shown <- FileSystemPath.value source.Path
            Runners.Search = fun _ view _ -> view hit }

    Shell.interpret deps ignore (RunSearch(searchFor FromDirectory, OnDisk(fp (at "start")), []))

    Assert.Equal(at "start/notes.txt", shown)

let private searched text : Ports.FindAsked =
    { Choice =
        Search
            { What = FindWhat.create (NameMasks.read "*.fs") None FilesOnly
              Scope = FromDirectory
              Hidden = Concealed
              Links = StopAtLinks
              Archives = SkipArchives }
      Masks = "*.fs"
      Text = text }

[<Fact>]
let ``the find popup offers the masks and the texts typed before`` () =
    let fs = MockFileSystem()
    typedEarlier fs FindMasks [ "*.md"; "*.fs" ]
    typedEarlier fs SearchedText [ "needle" ]
    let offered = ResizeArray<EnteredText list * EnteredText list>()

    let deps =
        { withFs fs with Prompts.AskFind = fun request -> offered.Add(request.Masks, request.Texts); None }

    Shell.interpret deps ignore (PromptForFind(NoDirectoryMarked, Concealed, None))

    Assert.Equal(([ typed "*.fs"; typed "*.md" ], [ typed "needle" ]), Assert.Single offered)

[<Fact>]
let ``a search confirmed remembers its masks and its text as they were typed`` () =
    let fs = MockFileSystem()
    let deps = { withFs fs with Prompts.AskFind = fun _ -> Some(searched "needle ") }

    Shell.interpret deps ignore (PromptForFind(NoDirectoryMarked, Concealed, None))

    Assert.Equal<string list>([ "*.fs" ], rememberedIn fs FindMasks)
    Assert.Equal<string list>([ "needle " ], rememberedIn fs SearchedText)

[<Fact>]
let ``a search for names alone remembers no text`` () =
    let fs = MockFileSystem()
    let deps = { withFs fs with Prompts.AskFind = fun _ -> Some(searched "") }

    Shell.interpret deps ignore (PromptForFind(NoDirectoryMarked, Concealed, None))

    Assert.Empty(rememberedIn fs SearchedText)

[<Fact>]
let ``asking for the last results remembers nothing`` () =
    let fs = MockFileSystem()

    let deps =
        { withFs fs with
            Prompts.AskFind =
                fun _ ->
                    Some
                        { searched "needle" with
                            Choice = LastResults } }

    Shell.interpret deps ignore (PromptForFind(NoDirectoryMarked, Concealed, Some 3))

    Assert.False(fs.File.Exists(at "history.conf"))

[<Fact>]
let ``the pack popup offers the archives packed into before`` () =
    let fs = MockFileSystem()
    typedEarlier fs ArchiveTarget [ @"D:\one.zip" ]
    let offered = ResizeArray<EnteredText list>()
    let deps = { withFs fs with Prompts.AskArchive = fun request -> offered.Add request.Remembered; None }

    Shell.interpret deps ignore (PromptForArchive(fp (at "backup/notes.zip")))

    Assert.Equal<EnteredText list>([ typed @"D:\one.zip" ], Assert.Single offered)

[<Fact>]
let ``an archive confirmed remembers where it was packed and never its password`` () =
    let fs = MockFileSystem()

    let packed =
        { Target = @"D:\one.zip"
          Format = ArchiveFormat.initial
          Compression = Normal
          Password = ArchivePassword.tryCreate "hunter2" }

    let deps = { withFs fs with Prompts.AskArchive = fun _ -> Some packed }

    Shell.interpret deps ignore (PromptForArchive(fp (at "backup/notes.zip")))

    Assert.Equal<string list>([ @"D:\one.zip" ], rememberedIn fs ArchiveTarget)
    Assert.DoesNotContain("hunter2", fs.File.ReadAllText(at "history.conf"))

[<Fact>]
let ``the link popup offers the places linked at before`` () =
    let fs = MockFileSystem()
    typedEarlier fs LinkDestination [ @"D:\links\one" ]
    let offered = ResizeArray<EnteredText list>()
    let deps = { withFs fs with Prompts.AskLink = fun request -> offered.Add request.Remembered; None }

    Shell.interpret deps ignore (PromptForLink([ fp (at "start/notes.txt") ], fp (at "backup"), fp (at "backup/notes.txt")))

    Assert.Equal<EnteredText list>([ typed @"D:\links\one" ], Assert.Single offered)

[<Fact>]
let ``a link confirmed remembers where it was made`` () =
    let fs = MockFileSystem()
    let deps = { withFs fs with Prompts.AskLink = fun _ -> Some { Target = @"D:\links\one"; Kind = Symbolic } }

    Shell.interpret deps ignore (PromptForLink([ fp (at "start/notes.txt") ], fp (at "backup"), fp (at "backup/notes.txt")))

    Assert.Equal<string list>([ @"D:\links\one" ], rememberedIn fs LinkDestination)

[<Fact>]
let ``the open-with popup offers the programs chosen before`` () =
    let fs = MockFileSystem()
    typedEarlier fs OpenWithProgram [ "code.exe" ]
    let offered = ResizeArray<EnteredText list>()
    let deps = { withFs fs with Prompts.AskHandler = fun request -> offered.Add request.Remembered; None }

    Shell.interpret deps ignore (ShowHandlers(fp (at "start/page.html"), None))

    Assert.Equal<EnteredText list>([ typed "code.exe" ], Assert.Single offered)

[<Fact>]
let ``a program confirmed in the open-with popup is remembered`` () =
    let fs = MockFileSystem()

    let choice =
        { Program = Expect.some (ProgramPath.tryCreate "firefox.exe")
          Use = ThisTimeOnly }

    let deps = { withFs fs with Prompts.AskHandler = fun _ -> Some choice }

    Shell.interpret deps ignore (ShowHandlers(fp (at "start/page.html"), None))

    Assert.Equal<string list>([ "firefox.exe" ], rememberedIn fs OpenWithProgram)

[<Fact>]
let ``popups of the larger dialogs cancelled remember nothing`` () =
    let fs = MockFileSystem()
    let deps = withFs fs

    Shell.interpret deps ignore (PromptForFind(NoDirectoryMarked, Concealed, None))
    Shell.interpret deps ignore (PromptForArchive(fp (at "backup/notes.zip")))
    Shell.interpret deps ignore (PromptForLink([ fp (at "start/notes.txt") ], fp (at "backup"), fp (at "backup/notes.txt")))
    Shell.interpret deps ignore (ShowHandlers(fp (at "start/page.html"), None))

    Assert.False(fs.File.Exists(at "history.conf"))

let private favoriteSlot digit =
    Expect.some (FavoriteSlot.tryCreate digit)

let private kept (deps: Ports.Dependencies) =
    Favorites.load deps.FileSystem deps.FavoritesPath |> Expect.ok

[<Fact>]
let ``a slot answered for a favourite keeps the place in it`` () =
    let deps = { noDeps () with Prompts.AskFavoriteSlot = fun _ -> Some(favoriteSlot 3) }

    Shell.interpret deps ignore (PromptForFavoriteSlot(Place.OnDisk(fp (at "work"))))

    Assert.Equal(Some(Place.OnDisk(fp (at "work"))), Favorites.placeIn (favoriteSlot 3) (kept deps))

[<Fact>]
let ``the slot is asked for with the place it will keep`` () =
    let asked = ResizeArray<Place>()
    let deps = { noDeps () with Prompts.AskFavoriteSlot = fun place -> asked.Add place; None }

    Shell.interpret deps ignore (PromptForFavoriteSlot(Place.OnDisk(fp (at "work"))))

    Assert.Equal<Place list>([ Place.OnDisk(fp (at "work")) ], List.ofSeq asked)

[<Fact>]
let ``a favourite asked for and not answered keeps nothing`` () =
    let deps = noDeps ()

    Shell.interpret deps ignore (PromptForFavoriteSlot(Place.OnDisk(fp (at "work"))))

    Assert.False(deps.FileSystem.File.Exists(FileSystemPath.value deps.FavoritesPath))

[<Fact>]
let ``favourites that cannot be written are reported`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "blocked", MockFileData "x")
    let mutable reported = ""

    let deps =
        { Fakes.dependencies fs (at "start") with
            FavoritesPath = fp (at "blocked/favorites.conf")
            Prompts.AskFavoriteSlot = fun _ -> Some(favoriteSlot 3)
            Prompts.ReportError = fun report -> reported <- report.Title }

    Shell.interpret deps ignore (PromptForFavoriteSlot(Place.OnDisk(fp (at "work"))))

    Assert.Equal(FavoritesErrorMessage.Title, reported)

[<Fact>]
let ``a favourite looked up is dispatched as found`` () =
    let sent, dispatch = capture ()
    let deps = noDeps ()

    Favorites.change deps.FileSystem deps.FavoritesPath (Keep(favoriteSlot 5, Place.OnDisk(fp (at "work"))))
    |> Expect.ok
    |> ignore

    Shell.interpret deps dispatch (LookUpFavorite(favoriteSlot 5))

    Assert.Equal<Message list>([ FavoriteFound(Place.OnDisk(fp (at "work"))) ], List.ofSeq sent)

[<Fact>]
let ``an empty slot looked up dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (LookUpFavorite(favoriteSlot 5))

    Assert.Empty sent

[<Fact>]
let ``favourites that cannot be read are reported when looked up`` () =
    let fs = Fakes.UnreadableFile(at "favorites.conf")
    fs.AddFile(at "favorites.conf", MockFileData "favorite.5=/work")
    let mutable reported = ""

    let deps =
        { Fakes.dependencies fs (at "start") with
            Prompts.ReportError = fun report -> reported <- report.Title }

    Shell.interpret deps ignore (LookUpFavorite(favoriteSlot 5))

    Assert.Equal(FavoritesErrorMessage.Title, reported)
