module Marbas.Ui.MainWindow

open Terminal.Gui.App
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
    let left, right = FilePanel.createPair ()
    let statusLine = StatusLine.create ()

    let views = [ AppState.Left, left; AppState.Right, right ]

    let render (state: AppState.AppState) =
        for side, view in views do
            FilePanel.render (AppState.panel side state) view

        let entry = state |> AppState.activePanel |> AppState.cursorEntry
        statusLine |> StatusLine.render entry

    let step msg state =
        let result = Update.update fs msg state
        result.State, result.Effects

    let appState = deps.StartPath |> FileSystemPath.create |> AppState.create
    let runtime = Runtime(appState, step, Shell.interpret deps, render)

    let dispatch = runtime.Dispatch

    let bar =
        FunctionKeyBar.create
            { OnCopy = ignore
              OnRenameMove = ignore
              OnMakeDirectory = fun () -> dispatch Update.MakeDirectoryRequested
              OnDelete = fun () -> dispatch Update.DeleteRequested
              OnQuit = fun () -> dispatch Update.QuitRequested }

    window.Add(left, right, statusLine, bar) |> ignore

    for side, _ in views do
        dispatch (Update.PathOpened(side, deps.StartPath))

    for side, view in views do
        let list = FilePanel.listView view
        let cursor () = list.SelectedItem.GetValueOrDefault 0

        list.FrameChanged.Add(fun _ -> runtime.Refresh())

        list.ValueChanged.Add(fun args ->
            if not runtime.IsRendering then
                dispatch (Update.CursorMoved(side, args.NewValue.GetValueOrDefault 0)))

        view.HasFocusChanged.Add(fun _ ->
            if view.HasFocus then
                dispatch (Update.PanelFocused side))

        list.Accepting.Add(fun args ->
            args.Handled <- true
            dispatch (Update.EntryOpened(side, cursor ())))

        list.KeyDown.Add(fun key ->
            match PanelInput.messageFor side (cursor ()) key with
            | Some msg ->
                key.Handled <- true
                dispatch msg
            | None -> ())

    window
