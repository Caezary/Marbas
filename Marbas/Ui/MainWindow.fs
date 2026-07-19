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

    let mutable state = AppState.create startPath

    let render () =
        FilePanel.render state.Left left
        FilePanel.render state.Right right

    let apply result =
        match result with
        | Ok updated -> state <- updated
        | Error _ -> ()

    apply (AppState.loadPanel fs AppState.Left startPath state)
    apply (AppState.loadPanel fs AppState.Right startPath state)
    render ()

    for view, side in [ left, AppState.Left; right, AppState.Right ] do
        (FilePanel.listView view).FrameChanged.Add(fun _ -> render ())

        view.HasFocusChanged.Add(fun _ ->
            if view.HasFocus then
                state <- AppState.activate side state)

    window
