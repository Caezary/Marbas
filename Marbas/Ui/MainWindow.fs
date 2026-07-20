module Marbas.Ui.MainWindow

open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.State

let quitKey = Key.Q.WithCtrl

let configureQuitKey () =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All(quitKey, Key.F10))

let create fs startPath onQuit =
    let window = new Window(Title = "Marbas", Width = Dim.Fill(), Height = Dim.Fill())
    let left, right = FilePanel.createPair ()
    let bar = FunctionKeyBar.create onQuit
    window.Add(left, right, bar) |> ignore

    let views = [ AppState.Left, left; AppState.Right, right ]

    let render (state: AppState.AppState) =
        for side, view in views do
            FilePanel.render (AppState.panel side state) view

    let store = Store.Store(AppState.create startPath, render)

    // Listing errors are ignored for now; P3.3 turns them into a message box.
    let ignoreError _ = ()

    for side, _ in views do
        store.DispatchResult ignoreError (AppState.loadPanel fs side startPath)

    for side, view in views do
        let list = FilePanel.listView view

        list.FrameChanged.Add(fun _ -> store.Refresh())

        view.HasFocusChanged.Add(fun _ ->
            if view.HasFocus then
                store.Dispatch(AppState.activate side))

        list.Accepting.Add(fun args ->
            args.Handled <- true

            store.DispatchResult ignoreError (fun state ->
                state
                |> AppState.activate side
                |> AppState.setCursor side (list.SelectedItem.GetValueOrDefault 0)
                |> AppState.enter fs))

    window
