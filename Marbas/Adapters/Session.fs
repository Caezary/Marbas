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

[<Literal>]
let private PrintedExitCode = 0

let private show (fs: IFileSystem) (env: HostEnvironment) (record: RecordLogEntry) () =
    MainWindow.configureQuitKey ()
    use app = Application.Create().Init()
    DialogStyle.apply ()
    HistoryField.apply app

    let dependencies =
        Dependencies.create app fs env (DebugLog.writer env.Logging record)

    use window = MainWindow.create dependencies

    app.Run window |> ignore

let private openPanels (fs: IFileSystem) (env: HostEnvironment) =
    let logPath =
        LogFile.beside fs (Settings.location fs env.ApplicationData env.UserProfile)

    LogFile.cut fs logPath |> ignore

    let oneWriter = obj ()

    let record entry =
        lock oneWriter (fun () -> LogEntry.write fs logPath DateTimeOffset.Now entry)

    use _crashes = watch env.Version record

    let runApplication = show fs env record

    match guarded env.Version record runApplication with
    | Closed -> ClosedExitCode
    | CrashWritten ->
        eprintfn "%s" (CrashMessage.written env.Version logPath)
        CrashExitCode
    | CrashUnwritten(error, failure) ->
        eprintfn "%s" (CrashMessage.unwritten env.Version error)
        eprintfn "%O" failure
        CrashExitCode

let private printVersion (env: HostEnvironment) =
    printfn "%s" (VersionMessage.product env.Version)
    PrintedExitCode

let run (fs: IFileSystem) (env: HostEnvironment) =
    match env.Launch with
    | OpenPanels -> openPanels fs env
    | PrintVersion -> printVersion env
