module Marbas.Adapters.SearchRunner

open System
open System.Collections.Concurrent
open System.IO.Abstractions
open System.Threading
open System.Threading.Tasks
open Terminal.Gui.App
open Marbas.FileSystem
open Marbas.Locations
open Marbas.Find
open Marbas.Find.FileSearch
open Marbas.State
open Marbas.Ui.Find
open Marbas.Ui.Help

let private Gathering = TimeSpan.FromMilliseconds 100.0

let private walk
    (app: IApplication)
    (fs: IFileSystem)
    (window: ResultsWindow.ResultsView)
    (cancellation: CancellationTokenSource)
    (showing: unit -> bool)
    query
    ground
    =
    let pending = ConcurrentQueue<FindReport>()

    let mutable walking =
        ground.Roots
        |> List.tryHead
        |> Option.map (PanelLocation.value fs)
        |> Option.defaultValue ""

    let control: Operations.BatchControl =
        { Report = fun path -> walking <- FileSystemPath.value path
          IsCancelled = fun () -> cancellation.IsCancellationRequested }

    let gather () =
        let arrived =
            [ let mutable report = Unchecked.defaultof<FindReport>

              while pending.TryDequeue &report do
                  yield report ]

        if showing () then
            ResultsWindow.add window arrived
            ResultsWindow.walking window walking

    let timer =
        app.AddTimeout(
            Gathering,
            (fun () ->
                gather ()
                true)
        )
        |> Option.ofObj

    Task.Run(fun () ->
        let outcome = FileSearch.run fs control pending.Enqueue query ground

        app.Invoke(fun () ->
            gather ()

            if showing () then
                ResultsWindow.finish window outcome))
    |> ignore

    timer

let run (app: IApplication) (fs: IFileSystem) (request: Ports.SearchRequest) =
    use cancellation = new CancellationTokenSource()
    let mutable showing = true

    let window =
        ResultsWindow.create fs request.Run request.View (fun () -> cancellation.Cancel()) _.RequestStop()
        |> HelpOpening.onHelpKey (fun () -> Prompts.showHelp app (HelpPlaces.ofPopup HelpPlaces.ResultsPopup))

    let timer =
        match request.Run with
        | Walk(query, ground) -> walk app fs window cancellation (fun () -> showing) query ground
        | Reopen _ -> None

    app.Run window |> ignore
    showing <- false
    cancellation.Cancel()
    timer |> Option.iter (app.RemoveTimeout >> ignore)
    request.Finish(ResultsWindow.answer window)
    window.Dispose()
