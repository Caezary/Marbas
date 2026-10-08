module Marbas.Adapters.BatchRunner

open System.Threading
open System.Threading.Tasks
open Terminal.Gui.App
open Marbas.FileSystem
open Marbas.Ui.Dialogs

let control
    (invoke: (unit -> unit) -> unit)
    (show: FileSystemPath -> unit)
    (isCancelled: unit -> bool)
    : Operations.BatchControl =
    { Report =
        fun path ->
            if not (isCancelled ()) then
                invoke (fun () ->
                    if not (isCancelled ()) then
                        show path)
      IsCancelled = isCancelled }

let private drive (app: IApplication) title (work: Operations.BatchControl -> 'a) (finish: 'a -> unit) =
    use cancellation = new CancellationTokenSource()
    use dialog = ProgressDialog.create title

    let control =
        control app.Invoke (FileSystemPath.value >> ProgressDialog.report dialog) (fun () ->
            cancellation.IsCancellationRequested)

    Task.Run(fun () ->
        let results = work control

        app.Invoke(fun () ->
            if not cancellation.IsCancellationRequested then
                dialog.RequestStop()

            finish results))
    |> ignore

    app.Run dialog |> ignore
    cancellation.Cancel()

let run app title work finish =
    drive app title (Operations.guarded title work) finish

let measured (work: Operations.BatchControl -> Measurement list) control =
    try
        Ok(work control)
    with ex ->
        Error(Operations.BatchFailed ex.Message)

let runMeasuring app title work finish = drive app title (measured work) finish
