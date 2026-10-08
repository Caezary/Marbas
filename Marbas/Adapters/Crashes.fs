module Marbas.Adapters.Crashes

open System
open System.Threading.Tasks
open Marbas.FileSystem
open Marbas.State

type SessionEnd =
    | Closed
    | CrashWritten
    | CrashUnwritten of LogFileError * exn

type RecordLogEntry = LogEntry -> Result<unit, LogFileError>

let guarded (record: RecordLogEntry) (session: unit -> unit) =
    try
        session ()
        Closed
    with failure ->
        match record (Crashed(UiThread, failure)) with
        | Ok() -> CrashWritten
        | Error error -> CrashUnwritten(error, failure)

let onUnhandled (record: RecordLogEntry) (args: UnhandledExceptionEventArgs) =
    record (Crashed(OtherThread, args.ExceptionObject :?> exn)) |> ignore

let onUnobserved (record: RecordLogEntry) (args: UnobservedTaskExceptionEventArgs) =
    record (Crashed(UnobservedTask, args.Exception)) |> ignore

let watch (record: RecordLogEntry) =
    let unhandled = UnhandledExceptionEventHandler(fun _ -> onUnhandled record)
    let unobserved = EventHandler<UnobservedTaskExceptionEventArgs>(fun _ -> onUnobserved record)
    AppDomain.CurrentDomain.UnhandledException.AddHandler unhandled
    TaskScheduler.UnobservedTaskException.AddHandler unobserved

    { new IDisposable with
        member _.Dispose() =
            AppDomain.CurrentDomain.UnhandledException.RemoveHandler unhandled
            TaskScheduler.UnobservedTaskException.RemoveHandler unobserved }
