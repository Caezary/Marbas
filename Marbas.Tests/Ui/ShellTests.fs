module Marbas.Tests.Ui.ShellTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui
open Marbas.Tests

let private fp = FileSystemPath.create

let private en = EntryName.create

let private noDeps () = Fakes.dependencies (MockFileSystem()) @"C:\start"

let private deleteRequest names scope = { Targets = names; Scope = scope }

let private cursor side index = { Side = side; Index = index }

let private capture () =
    let sent = ResizeArray<Message>()
    sent, fun (msg: Message) -> sent.Add msg

[<Fact>]
let ``a supplied name is dispatched as NameEntered`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with AskName = fun _ -> Some "work" }

    Shell.interpret deps dispatch PromptForName

    Assert.Equal<Message list>([ NameEntered(en "work") ], List.ofSeq sent)

[<Fact>]
let ``a name is trimmed before it becomes a message`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with AskName = fun _ -> Some "  work  " }

    Shell.interpret deps dispatch PromptForName

    Assert.Equal<Message list>([ NameEntered(en "work") ], List.ofSeq sent)

[<Fact>]
let ``a blank name never becomes a message`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with AskName = fun _ -> Some "   " }

    Shell.interpret deps dispatch PromptForName

    Assert.Empty sent

[<Fact>]
let ``a cancelled name prompt dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch PromptForName

    Assert.Empty sent

[<Fact>]
let ``the name prompt uses the mkdir label`` () =
    let _, dispatch = capture ()
    let mutable asked = ""
    let deps = { noDeps () with AskName = fun title -> asked <- title; None }

    Shell.interpret deps dispatch PromptForName

    Assert.Equal(Shell.MkdirPrompt, asked)

[<Fact>]
let ``a selecting pattern uses the select label and carries the context back`` () =
    let sent, dispatch = capture ()
    let mutable asked = ""
    let deps = { noDeps () with AskPattern = fun title -> asked <- title; Some "*.fs" }

    Shell.interpret deps dispatch (PromptForPattern(cursor Right 4, Select))

    Assert.Equal(Shell.SelectPrompt, asked)

    Assert.Equal<Message list>(
        [ PatternEntered(cursor Right 4, Select, NamePattern.tryCreate "*.fs" |> Expect.some) ],
        List.ofSeq sent
    )

[<Fact>]
let ``a deselecting pattern uses the deselect label`` () =
    let _, dispatch = capture ()
    let mutable asked = ""
    let deps = { noDeps () with AskPattern = fun title -> asked <- title; None }

    Shell.interpret deps dispatch (PromptForPattern(cursor Left 0, Deselect))

    Assert.Equal(Shell.DeselectPrompt, asked)

[<Fact>]
let ``an empty pattern never becomes a message`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with AskPattern = fun _ -> Some "" }

    Shell.interpret deps dispatch (PromptForPattern(cursor Left 0, Select))

    Assert.Empty sent

[<Fact>]
let ``a chosen drive is dispatched as an opened path`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with ChooseOption = fun _ _ -> Some @"D:\" }

    Shell.interpret deps dispatch (PromptForDrive Right)

    Assert.Equal<Message list>([ PathOpened(Right, fp @"D:\") ], List.ofSeq sent)

[<Fact>]
let ``the drive chooser is offered every mounted drive`` () =
    let _, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"
    fs.AddDirectory @"D:\data"
    let mutable offered = []
    let deps = { noDeps () with FileSystem = fs; ChooseOption = fun _ drives -> offered <- drives; None }

    Shell.interpret deps dispatch (PromptForDrive Left)

    Assert.Equal<string list>([ @"C:\"; @"D:\" ], offered)

[<Fact>]
let ``a listing error is reported under the listing title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with ReportError = fun title message -> reported <- (title, message) }

    Shell.interpret deps ignore (ReportListingError(DirectoryListing.DirectoryNotFound(fp @"C:\gone")))

    Assert.Equal(ListingErrorMessage.Title, fst reported)
    Assert.Contains(@"C:\gone", snd reported)

[<Fact>]
let ``an operation error is reported under the operation title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with ReportError = fun title message -> reported <- (title, message) }

    Shell.interpret deps ignore (ReportOperationError(TargetExists(fp @"C:\start\docs")))

    Assert.Equal(OperationErrorMessage.Title, fst reported)
    Assert.Contains("docs", snd reported)

[<Fact>]
let ``a confirmed delete is dispatched back`` () =
    let sent, dispatch = capture ()
    let deps = { noDeps () with Confirm = fun _ _ -> true }

    Shell.interpret deps dispatch (ConfirmDelete(deleteRequest [ en "notes.txt" ] NamedEntriesOnly))

    Assert.Equal<Message list>([ DeleteConfirmed ], List.ofSeq sent)

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
    Assert.Contains(DeleteConfirmation.NonEmptyWarning, snd asked)

[<Fact>]
let ``item failures are reported under the operation title`` () =
    let mutable reported = ("", "")
    let deps = { noDeps () with ReportError = fun title message -> reported <- (title, message) }

    let failed =
        [ { Name = EntryName.create "gone.txt"
            Outcome = Error(SourceNotFound(fp @"C:\start\gone.txt")) } ]

    Shell.interpret deps ignore (ReportItemFailures failed)

    Assert.Equal(OperationErrorMessage.Title, fst reported)
    Assert.Contains("gone.txt", snd reported)

let private copyReadyFs () =
    let fs = MockFileSystem()
    fs.AddFile(@"C:\start\notes.txt", MockFileData "new")
    fs.AddDirectory @"C:\backup"
    fs

[<Fact>]
let ``copy targets are copied and the outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = copyReadyFs ()
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (CopyTargets([ fp @"C:\start\notes.txt" ], fp @"C:\backup"))

    Assert.Equal("new", fs.File.ReadAllText @"C:\backup\notes.txt")
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

    Shell.interpret (deps ()) dispatch (CopyTargets([ fp @"C:\start\notes.txt" ], fp @"C:\backup"))
    Shell.interpret (deps ()) dispatch (MoveTargets([ fp @"C:\start\notes.txt" ], fp @"C:\backup"))

    Assert.Equal<string list>([ Shell.CopyTitle; Shell.MoveTitle ], List.ofSeq titles)

[<Fact>]
let ``nothing is run when every collision is refused`` () =
    let sent, dispatch = capture ()
    let fs = copyReadyFs ()
    fs.AddFile(@"C:\backup\notes.txt", MockFileData "old")
    let mutable started = false
    let deps =
        { noDeps () with
            FileSystem = fs
            AskOverwrite = (fun _ _ -> Operations.No)
            RunBatch = fun _ work finish -> started <- true; work Operations.unattended |> finish }

    Shell.interpret deps dispatch (CopyTargets([ fp @"C:\start\notes.txt" ], fp @"C:\backup"))

    Assert.False started
    Assert.Empty sent
    Assert.Equal("old", fs.File.ReadAllText @"C:\backup\notes.txt")

[<Fact>]
let ``a collision asks the overwrite question under its own title`` () =
    let _, dispatch = capture ()
    let fs = copyReadyFs ()
    fs.AddFile(@"C:\backup\notes.txt", MockFileData "old")
    let mutable asked = ("", "")
    let deps =
        { noDeps () with
            FileSystem = fs
            AskOverwrite = fun title message -> asked <- (title, message); Operations.No }

    Shell.interpret deps dispatch (CopyTargets([ fp @"C:\start\notes.txt" ], fp @"C:\backup"))

    Assert.Equal(OverwriteQuestion.Title, fst asked)
    Assert.Contains(@"C:\backup\notes.txt", snd asked)
    Assert.Equal("old", fs.File.ReadAllText @"C:\backup\notes.txt")

[<Fact>]
let ``the target prompt suggests a path and carries back what was typed`` () =
    let sent, dispatch = capture ()
    let mutable offered = ("", "")
    let deps =
        { noDeps () with
            AskTarget = fun title suggestion -> offered <- (title, suggestion); Some "readme.txt" }

    Shell.interpret deps dispatch (PromptForTarget(fp @"C:\backup"))

    Assert.Equal(Shell.MovePrompt, fst offered)
    Assert.Equal(@"C:\backup", snd offered)
    Assert.Equal<Message list>([ TargetEntered "readme.txt" ], List.ofSeq sent)

[<Fact>]
let ``a cancelled target prompt dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (PromptForTarget(fp @"C:\backup"))

    Assert.Empty sent

[<Fact>]
let ``move targets are moved and the outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = copyReadyFs ()
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (MoveTargets([ fp @"C:\start\notes.txt" ], fp @"C:\backup"))

    Assert.Equal("new", fs.File.ReadAllText @"C:\backup\notes.txt")
    Assert.False(fs.File.Exists @"C:\start\notes.txt")
    Assert.Equal<Message list>(
        [ MoveFinished [ { Name = EntryName.create "notes.txt"; Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``a rename is carried out and its outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = copyReadyFs ()
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (RenameTarget(fp @"C:\start\notes.txt", en "readme.txt"))

    Assert.Equal("new", fs.File.ReadAllText @"C:\start\readme.txt")
    Assert.Equal<Message list>([ RenameFinished(en "readme.txt", Ok()) ], List.ofSeq sent)

[<Fact>]
let ``quit runs the quit port`` () =
    let mutable quit = false
    let deps = { noDeps () with OnQuit = fun () -> quit <- true }

    Shell.interpret deps ignore Quit

    Assert.True quit
