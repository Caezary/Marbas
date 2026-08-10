module Marbas.Ui.MainWindow

open Terminal.Gui.App
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.State
open Marbas.State.Runtime

let quitKey = Key.Q.WithCtrl

let configureQuitKey () =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All(quitKey, Key.F10))

let create (deps: Shell.Dependencies) =
    let view = AppView.create ()
    let appState = deps.StartPath |> FileSystemPath.create |> AppState.create

    let runtime =
        Runtime(appState, Update.step deps.FileSystem, Shell.interpret deps, AppView.render view)

    let dispatch = runtime.Dispatch

    view.Window.Add(FunctionKeyBar.create dispatch) |> ignore

    for side, _ in AppView.panels view do
        dispatch (Update.PathOpened(side, deps.StartPath))

    let host: PanelWiring.Host =
        { Dispatch = dispatch
          Refresh = fun () -> runtime.Refresh()
          IsRendering = fun () -> runtime.IsRendering }

    for side, panel in AppView.panels view do
        PanelWiring.attach host side panel

    view.Window
