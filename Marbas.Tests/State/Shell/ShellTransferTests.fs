module Marbas.Tests.State.Shell.ShellTransferTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

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
        [ CopyFinished
              [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
                  Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``a batch is handed to the runner under its own title`` () =
    let _, dispatch = capture ()
    let titles = ResizeArray<string>()

    let deps () =
        { noDeps () with
            FileSystem = copyReadyFs ()
            Runners.Batch =
                fun request ->
                    titles.Add request.Title
                    request.Work unattended |> request.Finish }

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
            Runners.Batch =
                fun request ->
                    started <- true
                    request.Work unattended |> request.Finish }

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
            Prompts.AskOverwrite =
                fun request ->
                    asked.Add request.Target
                    No }

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
        [ MoveFinished
              [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
                  Outcome = Ok() } ] ],
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
            Runners.Batch =
                fun request ->
                    titles.Add request.Title
                    request.Work unattended |> request.Finish }

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
            Runners.Batch =
                fun request ->
                    titles.Add request.Title
                    request.Work unattended |> request.Finish }

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
            Runners.Batch = fun request -> request.Work stopped |> request.Finish }

    Shell.interpret deps dispatch (RenameTarget(fp (at "start/docs"), en "papers"))

    Assert.True(fs.Directory.Exists(at "start/docs"))

    Assert.Equal<Message list>([ RenameFinished(en "papers", Error(Cancelled(fp (at "start/docs")))) ], List.ofSeq sent)
