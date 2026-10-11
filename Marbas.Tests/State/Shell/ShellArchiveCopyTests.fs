module Marbas.Tests.State.Shell.ShellArchiveCopyTests

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
open Marbas.Tests.State.Shell.ShellArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

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
              [ { Source = ItemInArchive(dataZip, entry "notes.txt")
                  Outcome = Ok() }
                { Source = ItemInArchive(dataZip, entry "docs")
                  Outcome = Ok() } ]
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
                fun request ->
                    titles.Add request.Title
                    request.Work unattended |> request.Finish }

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
                fun request ->
                    batches.Add request.Title
                    request.Work unattended |> request.Finish }

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

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.AskPassword = fun _ -> None }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromArchive(dataZip, [ entry "notes.txt" ]) ], ToDirectory(fp (at "backup"))))

    Assert.False(fs.File.Exists(at "backup/notes.txt"))
    Assert.Empty sent
