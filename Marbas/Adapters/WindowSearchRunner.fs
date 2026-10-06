module Marbas.Adapters.WindowSearchRunner

open System.Threading
open System.Threading.Tasks
open Terminal.Gui.App
open Marbas.FileSystem
open Marbas.Ui.Dialogs

let control
    (invoke: (unit -> unit) -> unit)
    (show: string -> unit)
    (describe: int64 -> string)
    (isCancelled: unit -> bool)
    : WindowSearch.Control =
    let lastShown = ref None

    let showing text =
        lastShown.Value <- Some text
        invoke (fun () ->
            if not (isCancelled ()) then
                show text)

    { Reached =
        fun offset ->
            let text = describe offset

            if not (isCancelled ()) && lastShown.Value <> Some text then
                showing text
      IsCancelled = isCancelled }

let run (app: IApplication) title describe (work: WindowSearch.Control -> 'answer) =
    use cancellation = new CancellationTokenSource()
    use dialog = ProgressDialog.create title
    let isCancelled () = cancellation.IsCancellationRequested
    let control = control app.Invoke (ProgressDialog.report dialog) describe isCancelled

    let finishing () =
        if not (isCancelled ()) then
            dialog.RequestStop()

    let searching =
        Task.Run(fun () ->
            try
                work control
            finally
                app.Invoke finishing)

    app.Run dialog |> ignore
    cancellation.Cancel()
    searching.Result
