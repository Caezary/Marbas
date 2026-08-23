module Marbas.Ui.MainWindow

open Terminal.Gui.App
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.State
open Marbas.State.Runtime

let configureQuitKey () =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All(KeyMap.quitKey, Key.F10, KeyMap.cancelKey))

let create (deps: Shell.Dependencies) =
    let view = AppView.create ()

    let settings =
        Settings.load deps.FileSystem (Settings.forPath deps.StartPath) deps.SettingsPath

    let appState = AppState.fromSettings settings

    let runtime =
        Runtime(appState, Update.step deps.FileSystem, Shell.interpret deps, AppView.render view)

    let dispatch = runtime.Dispatch
    let keyBar = FunctionKeyBar.create dispatch

    view.Window.Add keyBar |> ignore

    for side, _ in AppView.panels view do
        dispatch (Update.PathOpened(side, (AppState.panel side appState).Path))

    let host: PanelWiring.Host =
        { Dispatch = dispatch
          Refresh = fun () -> runtime.Refresh()
          IsRendering = fun () -> runtime.IsRendering
          IsSearching = fun side -> (AppState.panel side runtime.State).Search |> Option.isSome }

    for side, panel in AppView.panels view do
        PanelWiring.attach host side panel

    deps.WatchModifiers(fun modifiers ->
        FunctionKeyBar.show keyBar modifiers
        runtime.Refresh())

    view.Window
