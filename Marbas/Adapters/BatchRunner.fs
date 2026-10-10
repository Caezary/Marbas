module Marbas.Adapters.BatchRunner

open System
open System.Threading
open System.Threading.Tasks
open Terminal.Gui.App
open Marbas.FileSystem
open Marbas.State
open Marbas.Ui.Dialogs

let private Showing = TimeSpan.FromMilliseconds 100.0

let control (latest: FileSystemPath option ref) (isCancelled: unit -> bool) : Operations.BatchControl =
    { Report = fun path -> Volatile.Write(&latest.contents, Some path)
      IsCancelled = isCancelled }

let showLatest (latest: FileSystemPath option ref) (show: FileSystemPath -> unit) =
    Interlocked.Exchange(&latest.contents, None) |> Option.iter show

let counting (count: unit -> unit) (control: Operations.BatchControl) : Operations.BatchControl =
    { control with
        Report =
            fun path ->
                count ()
                control.Report path }

let concluded title (isCancelled: unit -> bool) items =
    if isCancelled () then
        BatchCancelled(title, items)
    else
        BatchFinished(title, items)

let private drive
    (app: IApplication)
    (log: LogEntry -> unit)
    title
    (work: Operations.BatchControl -> 'a)
    (finish: 'a -> unit)
    =
    use cancellation = new CancellationTokenSource()
    use dialog = ProgressDialog.create title
    let reported = ref 0
    let latest = ref None
    let isCancelled () = cancellation.IsCancellationRequested

    let control =
        control latest isCancelled
        |> counting (fun () -> Interlocked.Increment &reported.contents |> ignore)

    let showing =
        app.AddTimeout(
            Showing,
            fun () ->
                showLatest latest (FileSystemPath.value >> ProgressDialog.report dialog)
                true
        )

    log (BatchStarted title)

    Task.Run(fun () ->
        let results = work control

        app.Invoke(fun () ->
            log (concluded title isCancelled (Volatile.Read &reported.contents))

            if not (isCancelled ()) then
                dialog.RequestStop()

            finish results))
    |> ignore

    app.Run dialog |> ignore
    app.RemoveTimeout showing |> ignore
    cancellation.Cancel()

let run app log title work finish =
    drive app log title (Operations.guarded title work) finish

let measured (work: Operations.BatchControl -> Measurement list) control =
    try
        Ok(work control)
    with ex ->
        Error(Operations.BatchFailed ex.Message)

let runMeasuring app log title work finish =
    drive app log title (measured work) finish

let runUnseen (work: unit -> unit) = Task.Run(work) |> ignore
