module Marbas.Adapters.DebugLog

open Marbas.State
open Marbas.Adapters.Crashes

let private recordingFailures (record: RecordLogEntry) entry =
    if LogEntry.isFailure entry then
        record entry |> ignore

let writer logging (record: RecordLogEntry) : LogEntry -> unit =
    match logging with
    | EveryTransition -> record >> ignore
    | FailuresOnly -> recordingFailures record
