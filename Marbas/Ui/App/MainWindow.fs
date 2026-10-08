module Marbas.Ui.App.MainWindow

open Terminal.Gui.App
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.State
open Marbas.State.Runtime
open Marbas.Ui.Keys
open Marbas.Ui.Panels

let configureQuitKey () =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All(KeyMap.quitKey, Key.F10, KeyMap.cancelKey))

let create (deps: Ports.Dependencies) =
    let view = AppView.create ()

    let settings =
        Settings.load deps.FileSystem (Settings.forPath deps.StartPath) deps.SettingsPath

    let appState = AppState.restore settings

    let runtime =
        Runtime(appState, Update.step deps.FileSystem, Shell.interpret deps, AppView.render deps.FileSystem view)

    let dispatch = runtime.Dispatch
    let keyBar = FunctionKeyBar.create dispatch

    view.Window.Add keyBar |> ignore

    for side, _ in AppView.panels view do
        (AppState.panel side appState).Path
        |> PanelLocation.directory
        |> Option.iter (fun path -> dispatch (Update.PathOpened(side, path)))

    runtime.State
    |> AppState.watchedLocations
    |> Update.WatchLocations
    |> Shell.interpret deps dispatch

    let host: PanelWiring.Host =
        { Dispatch = dispatch
          Refresh = fun () -> runtime.Refresh()
          IsRendering = fun () -> runtime.IsRendering
          IsSearching = fun side -> (AppState.panel side runtime.State).Search |> Option.isSome }

    for side, panel in AppView.panels view do
        PanelWiring.attach host side panel

    deps.Watches.Modifiers(FunctionKeyBar.show keyBar)

    view.Window
