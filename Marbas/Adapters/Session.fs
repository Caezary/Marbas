module Marbas.Adapters.Session

open System
open System.IO.Abstractions
open Terminal.Gui.App
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Messages
open Marbas.State
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Ui.App
open Marbas.Adapters.Crashes

[<Literal>]
let private ClosedExitCode = 0

[<Literal>]
let private CrashExitCode = 1

let private show (fs: IFileSystem) (env: HostEnvironment) (record: RecordLogEntry) () =
    MainWindow.configureQuitKey ()
    use app = Application.Create().Init()
    DialogStyle.apply ()
    HistoryField.apply app

    let dependencies =
        Dependencies.create app fs env (DebugLog.writer env.Logging record)

    use window = MainWindow.create dependencies

    app.Run window |> ignore

let run (fs: IFileSystem) (env: HostEnvironment) =
    let logPath =
        LogFile.beside fs (Settings.location fs env.ApplicationData env.UserProfile)

    LogFile.cut fs logPath |> ignore

    let oneWriter = obj ()

    let record entry =
        lock oneWriter (fun () -> LogEntry.write fs logPath DateTimeOffset.Now entry)

    use _crashes = watch record

    let runApplication = show fs env record

    match guarded record runApplication with
    | Closed -> ClosedExitCode
    | CrashWritten ->
        eprintfn "%s" (CrashMessage.written logPath)
        CrashExitCode
    | CrashUnwritten(error, failure) ->
        eprintfn "%s" (CrashMessage.unwritten error)
        eprintfn "%O" failure
        CrashExitCode
