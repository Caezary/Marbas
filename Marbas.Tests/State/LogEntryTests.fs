module Marbas.Tests.State.LogEntryTests

open System
open Xunit
open Marbas.State

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
