module Marbas.Tests.Adapters.CrashesTests

open System
open System.Collections.Concurrent
open System.Runtime.CompilerServices
open System.Threading.Tasks
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State
open Marbas.Adapters
open Marbas.Tests

let private at = Root.at

let private logPath = FileSystemPath.create (at "config/Marbas/marbas.log")

let private version = ProductVersion "0.1.0"

let private recorder () =
    let recorded = ConcurrentQueue<LogEntry>()

    let record entry =
        recorded.Enqueue entry
        Ok()

    recorded, record

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private abandonFailedTask (message: string) =
    Task.FromException(InvalidOperationException message) |> ignore

let private collectAbandonedTasks () =
    GC.Collect()
    GC.WaitForPendingFinalizers()
    GC.Collect()

let private unobservedMessages (recorded: LogEntry seq) =
    recorded
    |> Seq.collect (fun entry ->
        match entry with
        | Crashed(UnobservedTask, _, ex) -> [ ex.GetBaseException().Message ]
        | _ -> [])

[<Fact>]
let ``a session that ends on its own is closed and records nothing`` () =
    let recorded, record = recorder ()

    let ended = Crashes.guarded version record ignore

    Assert.Equal(Crashes.Closed, ended)
    Assert.Empty recorded

[<Fact>]
let ``a session that throws records the crash on the UI thread with the version`` () =
    let recorded, record = recorder ()
    let failure = InvalidOperationException "the loop broke"

    let ended = Crashes.guarded version record (fun () -> raise failure)

    Assert.Equal(Crashes.CrashWritten, ended)
    Assert.Equal<LogEntry>([ Crashed(UiThread, version, failure) ], recorded)

[<Fact>]
let ``a crash the log refuses is handed back with the reason`` () =
    let failure = InvalidOperationException "the loop broke"
    let refusing _ = Error(LogAccessDenied logPath)

    let ended = Crashes.guarded version refusing (fun () -> raise failure)

    Assert.Equal(Crashes.CrashUnwritten(LogAccessDenied logPath, failure), ended)

[<Fact>]
let ``an exception no thread caught is recorded as a crash on another thread`` () =
    let recorded, record = recorder ()
    let failure = InvalidOperationException "the watch broke"

    Crashes.onUnhandled version record (UnhandledExceptionEventArgs(failure, true))

    Assert.Equal<LogEntry>([ Crashed(OtherThread, version, failure) ], recorded)

[<Fact>]
let ``an exception in a task nobody awaited is recorded once the task is collected`` () =
    let recorded, record = recorder ()
    use _watch = Crashes.watch version record
    abandonFailedTask "the search broke"

    collectAbandonedTasks ()

    Assert.Contains("the search broke", unobservedMessages recorded)
