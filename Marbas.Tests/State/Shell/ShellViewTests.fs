module Marbas.Tests.State.Shell.ShellViewTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Archives
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.Viewing
open Marbas.Messages
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``a viewed file is handed over opened under its own path`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "first\r\nsecond")
    let mutable shown = ("", [])

    let readOut (source: FileWindow.Source) =
        let index = LineIndex.Index(source, source.Encoding)
        index.ScanAll()

        let lines =
            [ for line in 0 .. index.Known - 1 -> index.Text line |> Option.defaultValue "" ]

        shown <- (FileSystemPath.value source.Path, lines)

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.ShowFile = fun request -> readOut request.Source }

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

    let deps =
        { noDeps () with
            Processes.StartEditor =
                fun _ path ->
                    edited.Add path
                    Ok() }

    Shell.interpret deps dispatch (EditFile(fp (at "start/notes.txt"), None))

    Assert.Equal(fp (at "start/notes.txt"), Assert.Single edited)
    Assert.Equal<Message list>([ EditorStarted(Ok()) ], List.ofSeq sent)

[<Fact>]
let ``the editor the effect names is the one that is started`` () =
    let _, dispatch = capture ()
    let started = ResizeArray<ProgramPath option>()

    let deps =
        { noDeps () with
            Processes.StartEditor =
                fun chosen _ ->
                    started.Add chosen
                    Ok() }

    let chosen = ProgramPath.tryCreate "code.exe"

    Shell.interpret deps dispatch (EditFile(fp (at "start/notes.txt"), chosen))

    Assert.Equal(chosen, Assert.Single started)

[<Fact>]
let ``an editor that fails dispatches its failure back`` () =
    let sent, dispatch = capture ()
    let failure = OperationFailed(fp (at "start/notes.txt"), "no editor")

    let deps =
        { noDeps () with
            Processes.StartEditor = fun _ _ -> Error failure }

    Shell.interpret deps dispatch (EditFile(fp (at "start/notes.txt"), None))

    Assert.Equal<Message list>([ EditorStarted(Error failure) ], List.ofSeq sent)

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
        let index = LineIndex.Index(source, source.Encoding)
        index.ScanAll()

        let lines =
            [ for line in 0 .. index.Known - 1 -> index.Text line |> Option.defaultValue "" ]

        shown <- (FileSystemPath.value source.Path, lines)

    let deps =
        { noDeps () with
            FileSystem = fs
            Prompts.ShowFile = fun request -> readOut request.Source }

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
            Prompts.AskPassword =
                fun _ ->
                    asked <- asked + 1
                    ArchivePassword.tryCreate "opensesame"
            Prompts.ShowFile = fun request -> shown <- Text.Encoding.ASCII.GetString(request.Source.Read(0L, 6)) }

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

    Assert.False shown
