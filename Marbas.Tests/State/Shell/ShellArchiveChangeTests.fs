module Marbas.Tests.State.Shell.ShellArchiveChangeTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Archives
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

let private withLooseFile () =
    let fs = unpackReadyFs None
    fs.AddFile(at "backup/fresh.txt", MockFileData "fresh")
    fs

let private levelNames (fs: MockFileSystem) level =
    ArchiveListing.read fs dataZip level
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
        (CopyPlaces([ FromDisk [ fp (at "backup/fresh.txt") ] ], ToArchive(dataZip, ArchivePath.root)))

    Assert.Equal<string list>([ "docs"; "fresh.txt"; "notes.txt" ], levelNames fs ArchivePath.root)

    Assert.Equal<Message list>(
        [ CopyFinished
              [ { Source = ItemOnDisk(fp (at "backup/fresh.txt"))
                  Outcome = Ok() } ] ],
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
        (CopyPlaces([ FromDisk [ fp (at "backup/notes.txt") ] ], ToArchive(dataZip, ArchivePath.root)))

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
            Runners.Batch = fun request -> request.Work unattended |> request.Finish }

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
            Runners.Batch = fun request -> request.Work unattended |> request.Finish }

    Shell.interpret
        deps
        dispatch
        (CopyPlaces([ FromDisk [ fp (at "backup/fresh.txt") ] ], ToArchive(dataZip, ArchivePath.root)))

    Assert.Equal<Message list>(
        [ CopyFinished
              [ { Source = ItemOnDisk dataZip
                  Outcome = Error(ArchiveFailed(ProtectedArchiveCannotBeChanged dataZip)) } ] ],
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
                fun request ->
                    titles.Add request.Title
                    request.Work unattended |> request.Finish }

    Shell.interpret
        deps
        dispatch
        (MovePlaces([ FromDisk [ fp (at "backup/fresh.txt") ] ], ToArchive(dataZip, ArchivePath.root)))

    Assert.Equal<string list>([ "docs"; "fresh.txt"; "notes.txt" ], levelNames fs ArchivePath.root)
    Assert.False(fs.File.Exists(at "backup/fresh.txt"))
    Assert.Equal<string list>([ Shell.MoveTitle ], List.ofSeq titles)

    Assert.Equal<Message list>(
        [ MoveFinished
              [ { Source = ItemOnDisk(fp (at "backup/fresh.txt"))
                  Outcome = Ok() } ] ],
        List.ofSeq sent
    )

[<Fact>]
let ``an entry moved out of an archive is on disk and gone from the archive`` () =
    let sent, dispatch = capture ()
    let fs = unpackReadyFs None
    let entry = ArchivePath.tryCreate "notes.txt" |> Expect.some

    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (MovePlaces([ FromArchive(dataZip, [ entry ]) ], ToDirectory(fp (at "backup"))))

    Assert.Equal("notes", fs.File.ReadAllText(at "backup/notes.txt"))
    Assert.Equal<string list>([ "docs" ], levelNames fs ArchivePath.root)

    Assert.Equal<Message list>(
        [ MoveFinished
              [ { Source = ItemInArchive(dataZip, entry)
                  Outcome = Ok() } ] ],
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
                fun request ->
                    titles.Add request.Title
                    request.Work unattended |> request.Finish }

    Shell.interpret deps dispatch (DeletePlaces([ FromArchive(dataZip, [ entry ]) ], Permanently))

    Assert.Equal<string list>([ "docs" ], levelNames fs ArchivePath.root)
    Assert.Equal<string list>([ Shell.DeleteTitle ], List.ofSeq titles)

    Assert.Equal<Message list>(
        [ DeleteFinished
              [ { Source = ItemInArchive(dataZip, entry)
                  Outcome = Ok() } ] ],
        List.ofSeq sent
    )
