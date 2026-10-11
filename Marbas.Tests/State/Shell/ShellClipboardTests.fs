module Marbas.Tests.State.Shell.ShellClipboardTests

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
open Marbas.Tests.State.Shell.ShellArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private clipboardText lines =
    ClipboardText.tryCreate lines |> Expect.some

[<Fact>]
let ``text for the clipboard is handed to the clipboard`` () =
    let handed = ResizeArray<ClipboardText>()

    let deps =
        { noDeps () with
            Clipboard.SetText =
                fun text ->
                    handed.Add text
                    Ok() }

    Shell.interpret deps ignore (PutOnClipboard(clipboardText [ "docs"; "notes.txt" ]))

    Assert.Equal<ClipboardText list>([ clipboardText [ "docs"; "notes.txt" ] ], List.ofSeq handed)

[<Fact>]
let ``text the clipboard takes dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (noDeps ()) dispatch (PutOnClipboard(clipboardText [ "notes.txt" ]))

    Assert.Empty sent

[<Fact>]
let ``text the clipboard refuses is dispatched as unwritten`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Clipboard.SetText = fun _ -> Error ClipboardUnavailable }

    Shell.interpret deps dispatch (PutOnClipboard(clipboardText [ "notes.txt" ]))

    Assert.Equal<Message list>([ ClipboardRefused ClipboardUnavailable ], List.ofSeq sent)

[<Fact>]
let ``a clipboard error is reported under its title`` () =
    let mutable reported = ""

    let deps =
        { noDeps () with
            Prompts.ReportError = fun report -> reported <- report.Title }

    Shell.interpret deps ignore (Report(Problem.Clipboard ClipboardUnavailable))

    Assert.Equal(ClipboardErrorMessage.Title, reported)

let private holding (text: string option) =
    { noDeps () with
        Clipboard.GetText = fun () -> Ok text }

[<Fact>]
let ``a path on the clipboard is dispatched as read, forgiven against the directory given`` () =
    let sent, dispatch = capture ()

    Shell.interpret (holding (Some " \"docs\" ")) dispatch (ReadClipboardPath(fp (at "start")))

    Assert.Equal<Message list>([ ClipboardPathRead(fp (at "start/docs")) ], List.ofSeq sent)

[<Fact>]
let ``variables on the clipboard are expanded through the environment`` () =
    let sent, dispatch = capture ()

    let deps =
        { holding (Some(Root.relative "~/docs")) with
            Processes.EnvironmentVariable = fun _ -> Some(at "home/user") }

    Shell.interpret deps dispatch (ReadClipboardPath(fp (at "start")))

    Assert.Equal<Message list>([ ClipboardPathRead(fp (at "home/user/docs")) ], List.ofSeq sent)

[<Theory>]
[<InlineData("first\r\nsecond")>]
[<InlineData("   ")>]
let ``text on the clipboard that is no single path dispatches nothing`` (text: string) =
    let sent, dispatch = capture ()

    Shell.interpret (holding (Some text)) dispatch (ReadClipboardPath(fp (at "start")))

    Assert.Empty sent

[<Fact>]
let ``a clipboard holding no text dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (holding None) dispatch (ReadClipboardPath(fp (at "start")))

    Assert.Empty sent

[<Fact>]
let ``a clipboard that refuses to be read is dispatched as refused`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Clipboard.GetText = fun () -> Error ClipboardUnavailable }

    Shell.interpret deps dispatch (ReadClipboardPath(fp (at "start")))

    Assert.Equal<Message list>([ ClipboardRefused ClipboardUnavailable ], List.ofSeq sent)

let private clipboardFiles intent paths =
    ClipboardFiles.tryCreate intent (paths |> List.map fp) |> Expect.some

[<Fact>]
let ``files for the clipboard are handed to the clipboard with their intent`` () =
    let handed = ResizeArray<ClipboardFiles>()

    let deps =
        { noDeps () with
            Clipboard.SetFiles =
                fun files ->
                    handed.Add files
                    Ok() }

    Shell.interpret deps ignore (PutFilesOnClipboard(clipboardFiles ForMoving [ at "start/notes.txt" ]))

    Assert.Equal<ClipboardFiles list>([ clipboardFiles ForMoving [ at "start/notes.txt" ] ], List.ofSeq handed)

[<Fact>]
let ``files the clipboard refuses are dispatched as refused`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Clipboard.SetFiles = fun _ -> Error ClipboardUnavailable }

    Shell.interpret deps dispatch (PutFilesOnClipboard(clipboardFiles ForCopying [ at "start/notes.txt" ]))

    Assert.Equal<Message list>([ ClipboardRefused ClipboardUnavailable ], List.ofSeq sent)

let private forClipboard =
    [ FromDisk [ fp (at "start/work/notes.txt") ]
      FromArchive(dataZip, [ entry "notes.txt"; entry "docs" ]) ]

[<Fact>]
let ``entries for the clipboard are extracted into a scratch directory of the session`` () =
    let fs = unpackReadyFs None
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps ignore (ExtractForClipboard forClipboard)

    Assert.Equal("notes", fs.File.ReadAllText(at "temp/Marbas/session/1/notes.txt"))
    Assert.Equal("inner", fs.File.ReadAllText(at "temp/Marbas/session/1/docs/inner.txt"))

[<Fact>]
let ``what was extracted for the clipboard is dispatched back with the scratch directory`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            FileSystem = unpackReadyFs None }

    Shell.interpret deps dispatch (ExtractForClipboard forClipboard)

    Assert.Equal<Message list>(
        [ ClipboardExtractFinished(
              forClipboard,
              fp (at "temp/Marbas/session/1"),
              [ { Source = ItemInArchive(dataZip, entry "notes.txt")
                  Outcome = Ok() }
                { Source = ItemInArchive(dataZip, entry "docs")
                  Outcome = Ok() } ]
          ) ],
        List.ofSeq sent
    )

[<Fact>]
let ``extracting for the clipboard runs under the unpacking title`` () =
    let titles = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = unpackReadyFs None
            Runners.Batch =
                fun request ->
                    titles.Add request.Title
                    request.Work unattended |> request.Finish }

    Shell.interpret deps ignore (ExtractForClipboard forClipboard)

    Assert.Equal<string list>([ Shell.UnpackTitle ], List.ofSeq titles)

[<Fact>]
let ``a locked archive whose password is not given is left off the clipboard without a batch`` () =
    let sent, dispatch = capture ()
    let batches = ResizeArray<string>()

    let deps =
        { noDeps () with
            FileSystem = unpackReadyFs (ArchivePassword.tryCreate "letmein")
            Prompts.AskPassword = fun _ -> None
            Runners.Batch =
                fun request ->
                    batches.Add request.Title
                    request.Work unattended |> request.Finish }

    Shell.interpret deps dispatch (ExtractForClipboard forClipboard)

    Assert.Empty batches

    Assert.Equal<Message list>(
        [ ClipboardExtractFinished(forClipboard, fp (at "temp/Marbas/session/1"), []) ],
        List.ofSeq sent
    )

let private holdingFiles files =
    { noDeps () with
        Clipboard.GetFiles = fun () -> Ok files }

[<Fact>]
let ``files on the clipboard are dispatched as read, with the place to land in`` () =
    let sent, dispatch = capture ()
    let files = clipboardFiles ForMoving [ at "work/notes.txt" ]

    Shell.interpret (holdingFiles (Some files)) dispatch (ReadClipboardFiles(ToDirectory(fp (at "start"))))

    Assert.Equal<Message list>([ ClipboardFilesRead(files, ToDirectory(fp (at "start"))) ], List.ofSeq sent)

[<Fact>]
let ``a clipboard holding no files dispatches nothing`` () =
    let sent, dispatch = capture ()

    Shell.interpret (holdingFiles None) dispatch (ReadClipboardFiles(ToDirectory(fp (at "start"))))

    Assert.Empty sent

[<Fact>]
let ``a clipboard that refuses to give its files is dispatched as refused`` () =
    let sent, dispatch = capture ()

    let deps =
        { noDeps () with
            Clipboard.GetFiles = fun () -> Error ClipboardUnavailable }

    Shell.interpret deps dispatch (ReadClipboardFiles(ToDirectory(fp (at "start"))))

    Assert.Equal<Message list>([ ClipboardRefused ClipboardUnavailable ], List.ofSeq sent)

let private pasteReadyFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "work/notes.txt", MockFileData "notes")
    fs.AddFile(at "work/docs/inner.txt", MockFileData "inner")
    fs.AddDirectory(at "start")
    fs

let private cut = clipboardFiles ForMoving [ at "work/notes.txt"; at "work/docs" ]

let private emptying (emptied: ResizeArray<unit>) files (deps: Ports.Dependencies) =
    { deps with
        Clipboard.GetFiles = fun () -> Ok(Some files)
        Clipboard.Empty =
            fun () ->
                emptied.Add()
                Ok() }

[<Fact>]
let ``pasted files to be moved are moved and the outcome dispatched back`` () =
    let sent, dispatch = capture ()
    let fs = pasteReadyFs ()
    let deps = { noDeps () with FileSystem = fs }

    Shell.interpret deps dispatch (MovePastedFiles(cut, ToDirectory(fp (at "start"))))

    Assert.Equal("inner", fs.File.ReadAllText(at "start/docs/inner.txt"))
    Assert.False(fs.File.Exists(at "work/notes.txt"))

    Assert.Equal<Message list>(
        [ MoveFinished(
              [ Operations.itemResult (fp (at "work/notes.txt")) (Ok())
                Operations.itemResult (fp (at "work/docs")) (Ok()) ]
          ) ],
        List.ofSeq sent
    )

[<Fact>]
let ``the clipboard is emptied once the files on it were moved`` () =
    let emptied = ResizeArray<unit>()

    let deps =
        { noDeps () with
            FileSystem = pasteReadyFs () }
        |> emptying emptied cut

    Shell.interpret deps ignore (MovePastedFiles(cut, ToDirectory(fp (at "start"))))

    Assert.Single emptied

[<Fact>]
let ``the clipboard is kept when nothing was moved`` () =
    let emptied = ResizeArray<unit>()
    let fs = pasteReadyFs ()
    fs.AddFile(at "start/notes.txt", MockFileData "old")
    fs.AddDirectory(at "start/docs")

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.AskOverwrite = fun _ -> No }
        |> emptying emptied cut

    Shell.interpret deps ignore (MovePastedFiles(cut, ToDirectory(fp (at "start"))))

    Assert.Empty emptied

[<Fact>]
let ``the clipboard is kept when it no longer holds the files that were moved`` () =
    let emptied = ResizeArray<unit>()
    let other = clipboardFiles ForCopying [ at "elsewhere/report.txt" ]

    let deps =
        { noDeps () with
            FileSystem = pasteReadyFs () }
        |> emptying emptied other

    Shell.interpret deps ignore (MovePastedFiles(cut, ToDirectory(fp (at "start"))))

    Assert.Empty emptied

[<Fact>]
let ``pasted files to be moved into an archive are added to it and leave the disk`` () =
    let fs = pasteReadyFs ()

    Archive.write
        fs
        unattended
        { Path = fp (at "start/data.zip")
          Format = zipFormat
          Compression = Normal
          Password = None }
        [ fp (at "work/notes.txt") ]
    |> ignore

    fs.File.WriteAllText(at "work/notes.txt", "newer")

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.AskOverwrite = fun _ -> Yes }

    Shell.interpret deps ignore (MovePastedFiles(cut, ToArchive(fp (at "start/data.zip"), ArchivePath.root)))

    let held =
        ArchiveListing.read fs (fp (at "start/data.zip")) ArchivePath.root
        |> Expect.ok
        |> List.map (_.Name >> EntryName.value)

    Assert.Equal<string list>([ "docs"; "notes.txt" ], held |> List.sort)
    Assert.False(fs.File.Exists(at "work/notes.txt"))
    Assert.False(fs.Directory.Exists(at "work/docs"))
