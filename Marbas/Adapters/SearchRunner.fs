module Marbas.Adapters.SearchRunner

open System
open System.Collections.Concurrent
open System.IO.Abstractions
open System.Threading
open System.Threading.Tasks
open Terminal.Gui.App
open Marbas.FileSystem
open Marbas.Ui.Find

let private Gathering = TimeSpan.FromMilliseconds 100.0

let private walk
    (app: IApplication)
    (fs: IFileSystem)
    (window: ResultsWindow.ResultsView)
    (cancellation: CancellationTokenSource)
    query
    ground
    =
    let pending = ConcurrentQueue<FileSearch.FindHit>()

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
            [ let mutable hit = Unchecked.defaultof<FileSearch.FindHit>

              while pending.TryDequeue &hit do
                  yield hit ]

        ResultsWindow.add window arrived
        ResultsWindow.walking window walking

    let timer = app.AddTimeout(Gathering, (fun () -> gather (); true))

    Task.Run(fun () ->
        let outcome = FileSearch.run fs control pending.Enqueue query ground

        app.Invoke(fun () ->
            gather ()
            ResultsWindow.finish window outcome))
    |> ignore

    timer

let run (app: IApplication) (fs: IFileSystem) (asked: SearchRun) (view: FileSearch.FindHit -> unit) (finished: FindAnswer -> unit) =
    let cancellation = new CancellationTokenSource()

    let window =
        ResultsWindow.create fs asked view (fun () -> cancellation.Cancel()) _.RequestStop()

    let timer =
        match asked with
        | Walk(query, ground) -> walk app fs window cancellation query ground |> Some
        | Reopen _ -> None

    app.Run window |> ignore
    cancellation.Cancel()
    timer |> Option.iter (app.RemoveTimeout >> ignore)
    finished (ResultsWindow.answer window)
    window.Dispose()
