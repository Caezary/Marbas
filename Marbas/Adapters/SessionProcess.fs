module Marbas.Adapters.SessionProcess

open System
open System.ComponentModel
open System.Diagnostics
open Marbas.FileSystem

let private StartTolerance = TimeSpan.FromSeconds 2.0

let sameStart (written: DateTimeOffset) (read: DateTimeOffset) =
    (read - written).Duration() <= StartTolerance

let current () =
    use own = Process.GetCurrentProcess()

    { ProcessId = own.Id
      Started = DateTimeOffset own.StartTime }

let isRunning (owner: SessionOwner) =
    try
        use found = Process.GetProcessById owner.ProcessId
        sameStart owner.Started (DateTimeOffset found.StartTime)
    with
    | :? ArgumentException
    | :? InvalidOperationException -> false
    | :? Win32Exception -> true
