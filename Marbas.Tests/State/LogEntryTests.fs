module Marbas.Tests.State.LogEntryTests

open System
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State
open Marbas.State.Update
open Marbas.Tests

let private moment = DateTimeOffset(2026, 10, 8, 14, 30, 0, TimeSpan.FromHours 2.0)

let private stamp = "2026-10-08T14:30:00.0000000+02:00"

let private thrown () =
    try
        raise (InvalidOperationException "the loop broke")
    with ex ->
        ex

[<Fact>]
let ``a crash on the UI thread is stamped and names the exception`` () =
    let entry = Crashed(UiThread, InvalidOperationException "the loop broke")

    let lines = LogEntry.lines moment entry

    Assert.Equal<string>([ $"{stamp} crash on the UI thread: System.InvalidOperationException: the loop broke" ], lines)

[<Fact>]
let ``a crash on another thread says where it happened`` () =
    let entry = Crashed(OtherThread, InvalidOperationException "the watch broke")

    let lines = LogEntry.lines moment entry

    Assert.StartsWith($"{stamp} crash on another thread: ", List.head lines)

[<Fact>]
let ``a crash in a task nobody awaited says where it happened`` () =
    let entry = Crashed(UnobservedTask, InvalidOperationException "the search broke")

    let lines = LogEntry.lines moment entry

    Assert.StartsWith($"{stamp} crash in a task nobody awaited: ", List.head lines)

[<Fact>]
let ``a crash carries its stack trace, every line of it stamped`` () =
    let entry = Crashed(UiThread, thrown ())

    let lines = LogEntry.lines moment entry

    Assert.Contains(lines, fun line -> line.StartsWith $"{stamp}    at Marbas.Tests.State.LogEntryTests.thrown")
    Assert.All(lines, fun line -> Assert.StartsWith(stamp, line))

let private password = "open-sesame"

let private archivePassword = Expect.some (ArchivePassword.tryCreate password)

let private startState = AppState.create (FileSystemPath.create (Root.at "start"))

[<Fact>]
let ``a handled message is written by its case name and the panel it concerns`` () =
    let entry = MessageHandled(HiddenToggled, Left)

    let lines = LogEntry.lines moment entry

    Assert.Equal<string>([ $"{stamp} message HiddenToggled on the left panel" ], lines)

[<Fact>]
let ``a message concerns the active panel`` () =
    let entry = LogEntry.handling HiddenToggled startState

    Assert.Equal(MessageHandled(HiddenToggled, Left), entry)

[<Fact>]
let ``a message that names a panel concerns that panel`` () =
    let entry = LogEntry.handling (PanelFocused Right) startState

    Assert.Equal(MessageHandled(PanelFocused Right, Right), entry)

[<Fact>]
let ``an interpreted effect is written by its case name alone`` () =
    let entry =
        EffectInterpreted(ViewFile(FileSystemPath.create (Root.at "start/notes.txt")))

    let lines = LogEntry.lines moment entry

    Assert.Equal<string>([ $"{stamp} effect ViewFile" ], lines)

[<Fact>]
let ``a started batch is written with its title`` () =
    let lines = LogEntry.lines moment (BatchStarted "Copying")

    Assert.Equal<string>([ $"{stamp} batch started: Copying" ], lines)

[<Fact>]
let ``a finished batch is written with the items it reported`` () =
    let lines = LogEntry.lines moment (BatchFinished("Copying", 12))

    Assert.Equal<string>([ $"{stamp} batch finished: Copying, 12 items" ], lines)

[<Fact>]
let ``a cancelled batch is written with the items it reported before it stopped`` () =
    let lines = LogEntry.lines moment (BatchCancelled("Copying", 3))

    Assert.Equal<string>([ $"{stamp} batch cancelled: Copying, 3 items" ], lines)

[<Fact>]
let ``an error shown to the user is written with its title and its words`` () =
    let lines =
        LogEntry.lines moment (ErrorReported("Cannot copy", "Access denied to 'notes.txt'."))

    Assert.Equal<string>([ $"{stamp} error shown: Cannot copy: Access denied to 'notes.txt'." ], lines)

[<Fact>]
let ``a message carrying an archive password does not write it`` () =
    let choice: ArchiveChoice =
        { Target = "backup.zip"
          Format = List.head ArchiveFormat.all
          Compression = Normal
          Password = Some archivePassword }

    let lines = LogEntry.lines moment (MessageHandled(ArchiveEntered choice, Left))

    Assert.DoesNotContain(lines, fun line -> line.Contains password)

[<Fact>]
let ``an effect carrying an archive password does not write it`` () =
    let request: ArchiveRequest =
        { Path = FileSystemPath.create (Root.at "start/backup.zip")
          Format = List.head ArchiveFormat.all
          Compression = Normal
          Password = Some archivePassword }

    let lines = LogEntry.lines moment (EffectInterpreted(ArchiveTargets([], request)))

    Assert.DoesNotContain(lines, fun line -> line.Contains password)

[<Fact>]
let ``an effect carrying clipboard text does not write it`` () =
    let text = Expect.some (ClipboardText.tryCreate [ "the clipboard text" ])

    let lines = LogEntry.lines moment (EffectInterpreted(PutOnClipboard text))

    Assert.DoesNotContain(lines, fun line -> line.Contains "the clipboard text")

[<Fact>]
let ``a cursor move concerns the panel its cursor is in`` () =
    let entry = LogEntry.handling (CursorMoved { Side = Right; Index = 2 }) startState

    Assert.Equal(MessageHandled(CursorMoved { Side = Right; Index = 2 }, Right), entry)
