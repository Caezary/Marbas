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

let guarded version (record: RecordLogEntry) (session: unit -> unit) =
    try
        session ()
        Closed
    with failure ->
        match record (Crashed(UiThread, version, failure)) with
        | Ok() -> CrashWritten
        | Error error -> CrashUnwritten(error, failure)

let onUnhandled version (record: RecordLogEntry) (args: UnhandledExceptionEventArgs) =
    record (Crashed(OtherThread, version, args.ExceptionObject :?> exn)) |> ignore

let onUnobserved version (record: RecordLogEntry) (args: UnobservedTaskExceptionEventArgs) =
    record (Crashed(UnobservedTask, version, args.Exception)) |> ignore

let watch version (record: RecordLogEntry) =
    let unhandled = UnhandledExceptionEventHandler(fun _ -> onUnhandled version record)

    let unobserved =
        EventHandler<UnobservedTaskExceptionEventArgs>(fun _ -> onUnobserved version record)

    AppDomain.CurrentDomain.UnhandledException.AddHandler unhandled
    TaskScheduler.UnobservedTaskException.AddHandler unobserved

    { new IDisposable with
        member _.Dispose() =
            AppDomain.CurrentDomain.UnhandledException.RemoveHandler unhandled
            TaskScheduler.UnobservedTaskException.RemoveHandler unobserved }
