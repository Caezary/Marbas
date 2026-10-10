module Marbas.Ui.App.MainWindow

open Terminal.Gui.App
open Terminal.Gui.Input
open Marbas.State
open Marbas.State.Runtime
open Marbas.Ui.Keys
open Marbas.Ui.Panels

let configureQuitKey () =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All(KeyMap.quitKey, Key.F10, KeyMap.cancelKey))

let create (deps: Ports.Dependencies) =
    let view = AppView.create ()

    let runtime =
        Runtime(
            Startup.restored deps,
            Update.step deps.FileSystem,
            Shell.interpret deps,
            AppView.render deps.FileSystem view,
            fun message state -> deps.Log(LogEntry.handling message state)
        )

    let dispatch = runtime.Dispatch
    let keyBar = FunctionKeyBar.create dispatch

    view.Window.Add keyBar |> ignore

    Startup.firstSteps deps dispatch (fun () -> runtime.State)

    let host: PanelWiring.Host =
        { Dispatch = dispatch
          Refresh = fun () -> runtime.Refresh()
          IsRendering = fun () -> runtime.IsRendering
          IsSearching = fun side -> (AppState.panel side runtime.State).Search |> Option.isSome }

    for side, panel in AppView.panels view do
        PanelWiring.attach host side panel

    deps.Watches.Modifiers { Report = FunctionKeyBar.show keyBar }

    view.Window
