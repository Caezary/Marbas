module Marbas.Tests.State.Shell.ShellDeleteTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private deleteRequest names scope =
    { Targets = names
      Scope = scope
      Mode = ToTrash }

let private recording (reported: ResizeArray<FileSystemPath>) =
    { Report = reported.Add
      IsCancelled = fun () -> false }

[<Fact>]
let ``a confirmed delete is dispatched back`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Prompts.Confirm = fun _ -> true }

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

    let deps =
        { noDeps () with
            Prompts.Confirm =
                fun question ->
                    asked <- (question.Title, question.Question)
                    false }

    Shell.interpret deps dispatch (ConfirmDelete(deleteRequest [ en "docs" ] WithDirectoryContents))

    Assert.Equal(DeleteConfirmation.Title, fst asked)
    Assert.Contains(DeleteConfirmation.TrashWarning, snd asked)

[<Fact>]
let ``a permanent delete asks under its own title`` () =
    let _, dispatch = capture ()
    let mutable asked = ("", "")

    let deps =
        { noDeps () with
            Prompts.Confirm =
                fun question ->
                    asked <- (question.Title, question.Question)
                    false }

    let request =
        { deleteRequest [ en "notes.txt" ] NamedEntriesOnly with
            Mode = Permanently }

    Shell.interpret deps dispatch (ConfirmDelete request)

    Assert.Equal(DeleteConfirmation.PermanentTitle, fst asked)

[<Fact>]
let ``a confirmed permanent delete is dispatched back with its mode`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Prompts.Confirm = fun _ -> true }

    let request =
        { deleteRequest [ en "notes.txt" ] NamedEntriesOnly with
            Mode = Permanently }

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
            Processes.SendToTrash =
                fun path ->
                    trashed.Add path
                    Ok() }

    Shell.interpret deps dispatch (DeletePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ToTrash))

    Assert.Equal<FileSystemPath list>([ fp (at "start/notes.txt") ], List.ofSeq trashed)
    Assert.True(fs.File.Exists(at "start/notes.txt"))

    Assert.Equal<Message list>(
        [ DeleteFinished
              [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
                  Outcome = Ok() } ] ],
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
            Runners.Batch = fun request -> request.Work(recording reported) |> request.Finish }

    Shell.interpret
        deps
        dispatch
        (DeletePlaces([ FromDisk [ fp (at "start/notes.txt"); fp (at "start/readme.txt") ] ], ToTrash))

    Assert.Equal<FileSystemPath list>([ fp (at "start/notes.txt"); fp (at "start/readme.txt") ], List.ofSeq reported)

[<Fact>]
let ``a trash stopped with Esc leaves the entries it never reached`` () =
    let sent, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/readme.txt", MockFileData "hi")
    let trashed = ResizeArray<FileSystemPath>()

    let deps =
        { Fakes.dependencies fs (at "start") with
            Processes.SendToTrash =
                fun path ->
                    trashed.Add path
                    Ok()
            Runners.Batch = fun request -> request.Work stopped |> request.Finish }

    Shell.interpret
        deps
        dispatch
        (DeletePlaces([ FromDisk [ fp (at "start/notes.txt"); fp (at "start/readme.txt") ] ], ToTrash))

    Assert.Equal<FileSystemPath list>([ fp (at "start/notes.txt") ], List.ofSeq trashed)

    Assert.Equal<Message list>(
        [ DeleteFinished
              [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
                  Outcome = Ok() } ] ],
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
            Runners.Batch = fun request -> request.Work(recording reported) |> request.Finish }

    Shell.interpret deps dispatch (DeletePlaces([ FromDisk [ fp (at "start/docs") ] ], Permanently))

    Assert.Equal<FileSystemPath list>([ fp (at "start/docs"); fp (at "start/docs/inner.txt") ], List.ofSeq reported)

[<Fact>]
let ``a permanent delete stopped with Esc leaves the rest of the tree`` () =
    let _, dispatch = capture ()
    let fs = MockFileSystem()
    fs.AddFile(at "start/docs/inner.txt", MockFileData "x")

    let deps =
        { Fakes.dependencies fs (at "start") with
            Runners.Batch = fun request -> request.Work stopped |> request.Finish }

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
        [ DeleteFinished
              [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
                  Outcome = Ok() } ] ],
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
                fun request ->
                    titles.Add request.Title
                    request.Work unattended |> request.Finish }

    Shell.interpret deps dispatch (DeletePlaces([ FromDisk [ fp (at "start/notes.txt") ] ], ByWiping))

    Assert.Equal<string list>([ Shell.WipeTitle ], List.ofSeq titles)
    Assert.Empty(fs.Directory.GetFileSystemEntries(at "start"))

    Assert.Equal<Message list>(
        [ DeleteFinished
              [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
                  Outcome = Ok() } ] ],
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
            Runners.Batch = fun request -> request.Work stopped |> request.Finish }

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
        [ DeleteFinished
              [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
                  Outcome = Error refusal } ] ],
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
        (InspectDelete(
            { Probed = [ fp (at "start/docs") ]
              AtLeast = NamedEntriesOnly },
            ToTrash
        ))

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
        (InspectDelete(
            { Probed = [ fp (at "start/docs") ]
              AtLeast = NamedEntriesOnly },
            Permanently
        ))

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
        (InspectDelete(
            { Probed = [ fp (at "start/docs") ]
              AtLeast = WithDirectoryContents },
            Permanently
        ))

    Assert.Equal(0, fs.AskedAbout(at "start/docs"))
    Assert.Equal<Message list>([ DeleteScopeLoaded(WithDirectoryContents, Permanently) ], List.ofSeq sent)
