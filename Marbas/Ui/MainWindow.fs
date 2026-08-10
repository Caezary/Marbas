module Marbas.Ui.MainWindow

open Terminal.Gui.App
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.State
open Marbas.State.Runtime

let quitKey = Key.Q.WithCtrl

let configureQuitKey () =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All(quitKey, Key.F10))

let create (deps: Shell.Dependencies) =
    let fs = deps.FileSystem
    let window = new Window(Title = "Marbas", Width = Dim.Fill(), Height = Dim.Fill())
    window.BorderStyle <- LineStyle.None
    let left, right = FilePanel.createPair ()
    let statusLine = StatusLine.create ()

    let views = [ AppState.Left, left; AppState.Right, right ]

    let render (state: AppState.AppState) =
        for side, view in views do
            FilePanel.render (AppState.panel side state) view

        let entry = state |> AppState.activePanel |> AppState.cursorEntry
        statusLine |> StatusLine.render entry

    let appState = deps.StartPath |> FileSystemPath.create |> AppState.create
    let runtime = Runtime(appState, Update.step fs, Shell.interpret deps, render)

    let dispatch = runtime.Dispatch

    window.Add(left, right, statusLine, FunctionKeyBar.create dispatch) |> ignore

    for side, _ in views do
        dispatch (Update.PathOpened(side, deps.StartPath))

    let host: PanelWiring.Host =
        { Dispatch = dispatch
          Refresh = fun () -> runtime.Refresh()
          IsRendering = fun () -> runtime.IsRendering }

    for side, view in views do
        PanelWiring.attach host side view

    window
