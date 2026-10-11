module Marbas.Tests.State.Shell.ShellHistoryTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Domain
open Marbas.Messages
open Marbas.State
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private unwrittenHistory msg =
    match msg with
    | HistoryUnwritten(HistoryAccessDenied path)
    | HistoryUnwritten(HistoryFailed(path, _)) -> Some path
    | _ -> None

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

    let deps =
        { noDeps () with
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    Shell.interpret deps ignore (Report(Problem.History(HistoryAccessDenied(fp (at "config/history.conf")))))

    Assert.Equal(HistoryErrorMessage.Title, fst reported)
    Assert.Contains(at "config/history.conf", snd reported)

[<Fact>]
let ``the history is read afresh and offered on the tab asked for`` () =
    let fs = MockFileSystem()
    let now = DateTimeOffset(2026, 9, 28, 15, 30, 0, TimeSpan.FromHours 2.0)
    let path = fp (at "config/history.conf")

    let stored =
        History.change fs path now (Remember(Visited(Place.OnDisk(fp (at "work")))))
        |> Expect.ok

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
        asked
        |> Seq.map (fun prompt -> prompt.History, prompt.Tab, prompt.Now)
        |> List.ofSeq
    )

    Assert.Equal<Message list>(
        [ HistoryChanged stored
          HistoryAnswered { Tab = ViewHistoryTab; Choice = None } ],
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

    let seen =
        { Where = Place.OnDisk(fp (at "notes.md"))
          How = Viewed }

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

    let deps =
        changingFromPopup fs (fp (at "blocked/history.conf")) (ClearList FolderHistory) answers

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
