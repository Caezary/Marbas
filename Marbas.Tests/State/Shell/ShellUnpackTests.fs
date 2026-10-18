module Marbas.Tests.State.Shell.ShellUnpackTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Archives
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture
open Marbas.Tests.State.Shell.ShellArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

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
              [ { Source = ItemInArchive(dataZip, entry "notes.txt")
                  Outcome = Ok() }
                { Source = ItemInArchive(dataZip, entry "docs")
                  Outcome = Ok() } ]
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
                fun request ->
                    titles.Add request.Title
                    request.Work unattended |> request.Finish }

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
