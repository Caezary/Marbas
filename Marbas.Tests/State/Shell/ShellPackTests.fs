module Marbas.Tests.State.Shell.ShellPackTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Archives
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture
open Marbas.Tests.State.Shell.ShellArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private en = EntryName.create

let private packReadyFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "new")
    fs.AddDirectory(at "backup")
    fs

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
        [ PackFinished(
              en "notes.zip",
              [ { Source = ItemOnDisk(fp (at "start/notes.txt"))
                  Outcome = Ok() } ]
          ) ],
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
                fun request ->
                    titles.Add request.Title
                    request.Work unattended |> request.Finish }

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

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.Confirm = fun _ -> false }

    Shell.interpret deps dispatch (ArchiveTargets([ fp (at "start/notes.txt") ], packing (at "backup/notes.zip")))

    Assert.Empty sent

[<Fact>]
let ``an accepted overwrite replaces the archive`` () =
    let sent, dispatch = capture ()
    let fs = packReadyFs ()
    fs.AddFile(at "backup/notes.zip", MockFileData "old")

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.Confirm = fun _ -> true }

    Shell.interpret deps dispatch (ArchiveTargets([ fp (at "start/notes.txt") ], packing (at "backup/notes.zip")))

    let written = fs.File.ReadAllBytes(at "backup/notes.zip")

    Assert.Equal<byte list>([ 0x50uy; 0x4Buy ], written |> Array.truncate 2 |> List.ofArray)
    Assert.Single sent |> ignore
