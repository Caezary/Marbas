module Marbas.Adapters.DebugLog

open Marbas.State
open Marbas.Adapters.Crashes

let writer logging (record: RecordLogEntry) : LogEntry -> unit =
    match logging with
    | EveryTransition -> record >> ignore
    | CrashesOnly -> ignore
