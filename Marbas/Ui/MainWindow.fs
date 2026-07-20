module Marbas.Ui.MainWindow

open System.IO.Abstractions
open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.State

type Dependencies =
    { FileSystem: IFileSystem
      StartPath: string
      OnQuit: unit -> unit
      ReportError: string -> unit
      ChooseDrive: string list -> string option
      AskPattern: string -> string option }

let quitKey = Key.Q.WithCtrl

let refreshKey = Key.R.WithCtrl

let selectionKeys = [ Key.InsertChar; Key.Space ]

let selectPatternKey = Key '+'

let deselectPatternKey = Key '-'

[<Literal>]
let SelectPrompt = "Select"

[<Literal>]
let DeselectPrompt = "Deselect"

let leftDriveKey = Key.F1.WithAlt

let rightDriveKey = Key.F2.WithAlt

let configureQuitKey () =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All(quitKey, Key.F10))

let create (deps: Dependencies) =
    let fs = deps.FileSystem
    let window = new Window(Title = "Marbas", Width = Dim.Fill(), Height = Dim.Fill())
    let left, right = FilePanel.createPair ()
    let statusLine = StatusLine.create ()
    let bar = FunctionKeyBar.create deps.OnQuit
    window.Add(left, right, statusLine, bar) |> ignore

    let views = [ AppState.Left, left; AppState.Right, right ]

    let mutable rendering = false

    let render (state: AppState.AppState) =
        rendering <- true

        for side, view in views do
            FilePanel.render (AppState.panel side state) view

        statusLine |> StatusLine.render (AppState.cursorEntry (AppState.activePanel state))
        rendering <- false

    let store = Store.Store(AppState.create deps.StartPath, render)

    let report error =
        deps.ReportError(ListingErrorMessage.describe error)

    for side, _ in views do
        store.DispatchResult report (AppState.loadPanel fs side deps.StartPath)

    let openDrive side =
        match deps.ChooseDrive(Drives.list fs) with
        | Some drive -> store.DispatchResult report (AppState.loadPanel fs side drive)
        | None -> ()

    let driveKeySide (key: Key) =
        if key = leftDriveKey then Some AppState.Left
        elif key = rightDriveKey then Some AppState.Right
        else None

    for side, view in views do
        let list = FilePanel.listView view

        list.FrameChanged.Add(fun _ -> store.Refresh())

        list.ValueChanged.Add(fun args ->
            if not rendering then
                store.Dispatch(AppState.setCursor side (args.NewValue.GetValueOrDefault 0)))

        view.HasFocusChanged.Add(fun _ ->
            if view.HasFocus then
                store.Dispatch(AppState.activate side))

        let atCursor transition state =
            state
            |> AppState.activate side
            |> AppState.setCursor side (list.SelectedItem.GetValueOrDefault 0)
            |> transition

        let dispatchOnCursor transition =
            store.DispatchResult report (atCursor transition)

        list.Accepting.Add(fun args ->
            args.Handled <- true
            dispatchOnCursor (AppState.enter fs))

        list.KeyDown.Add(fun key ->
            if key = refreshKey then
                key.Handled <- true
                dispatchOnCursor (AppState.refresh fs)
            elif selectionKeys |> List.contains key then
                key.Handled <- true
                store.Dispatch(atCursor (AppState.toggleSelectionAndAdvance side))
            elif key = selectPatternKey || key = deselectPatternKey then
                key.Handled <- true
                let selecting = key = selectPatternKey

                match deps.AskPattern(if selecting then SelectPrompt else DeselectPrompt) with
                | Some pattern ->
                    let transition =
                        if selecting then
                            AppState.selectMatching side pattern
                        else
                            AppState.deselectMatching side pattern

                    store.Dispatch(atCursor transition)
                | None -> ()
            else
                match driveKeySide key with
                | Some target ->
                    key.Handled <- true
                    openDrive target
                | None -> ())

    window
