module Marbas.Adapters.BatchRunner

open System.Threading
open System.Threading.Tasks
open Terminal.Gui.App
open Marbas.FileSystem
open Marbas.Ui.Dialogs

let run (app: IApplication) title work finish =
    let cancellation = new CancellationTokenSource()
    let dialog = ProgressDialog.create title

    let control: Operations.BatchControl =
        { Report =
            fun path ->
                if not cancellation.IsCancellationRequested then
                    app.Invoke(fun () -> ProgressDialog.report dialog (FileSystemPath.value path))
          IsCancelled = fun () -> cancellation.IsCancellationRequested }

    Task.Run(fun () ->
        let results = Operations.guarded title work control

        app.Invoke(fun () ->
            if not cancellation.IsCancellationRequested then
                dialog.RequestStop()

            finish results))
    |> ignore

    app.Run dialog |> ignore
    cancellation.Cancel()
