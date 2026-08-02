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
      ReportError: string -> string -> unit
      ChooseOption: string -> string list -> string option
      AskPattern: string -> string option
      AskName: string -> string option }

let quitKey = Key.Q.WithCtrl

let refreshKey = Key.R.WithCtrl

let selectionKeys = [ Key.InsertChar; Key.Space ]

let selectPatternKey = Key '+'

let deselectPatternKey = Key '-'

[<Literal>]
let SelectPrompt = "Select"

[<Literal>]
let DeselectPrompt = "Deselect"

[<Literal>]
let MkdirPrompt = "Create directory"

[<Literal>]
let DrivePrompt = "Select drive"

let leftDriveKey = Key.F1.WithAlt

let rightDriveKey = Key.F2.WithAlt

let configureQuitKey () =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All(quitKey, Key.F10))

let create (deps: Dependencies) =
    let fs = deps.FileSystem
    let window = new Window(Title = "Marbas", Width = Dim.Fill(), Height = Dim.Fill())
    let left, right = FilePanel.createPair ()
    let statusLine = StatusLine.create ()

    let views = [ AppState.Left, left; AppState.Right, right ]

    let mutable rendering = false

    let render (state: AppState.AppState) =
        rendering <- true

        for side, view in views do
            FilePanel.render (AppState.panel side state) view

        let entry = state |> AppState.activePanel |> AppState.cursorEntry
        statusLine |> StatusLine.render entry
        rendering <- false

    let store = Store.Store(AppState.create (FileSystemPath.create deps.StartPath), render)

    let rec dispatch msg =
        let result = Update.update fs msg store.State
        store.Put result.State
        result.Effects |> List.iter runEffect

    and runEffect effect =
        match effect with
        | Update.ReportListingError error ->
            deps.ReportError ListingErrorMessage.Title (ListingErrorMessage.describe error)
        | Update.ReportOperationError error ->
            deps.ReportError OperationErrorMessage.Title (OperationErrorMessage.describe error)
        | Update.PromptForName ->
            match deps.AskName MkdirPrompt with
            | Some name -> dispatch (Update.NameEntered name)
            | None -> ()
        | Update.PromptForPattern(side, index, selecting) ->
            match deps.AskPattern (if selecting then SelectPrompt else DeselectPrompt) with
            | Some pattern -> dispatch (Update.PatternEntered(side, index, selecting, pattern))
            | None -> ()
        | Update.PromptForDrive side ->
            match deps.ChooseOption DrivePrompt (Drives.list fs) with
            | Some drive -> dispatch (Update.PathOpened(side, drive))
            | None -> ()
        | Update.Quit -> deps.OnQuit()

    let bar =
        FunctionKeyBar.create
            { OnCopy = ignore
              OnRenameMove = ignore
              OnMakeDirectory = fun () -> dispatch Update.MakeDirectoryRequested
              OnDelete = ignore
              OnQuit = fun () -> dispatch Update.QuitRequested }

    window.Add(left, right, statusLine, bar) |> ignore

    for side, _ in views do
        dispatch (Update.PathOpened(side, deps.StartPath))

    let driveKeySide (key: Key) =
        if key = leftDriveKey then Some AppState.Left
        elif key = rightDriveKey then Some AppState.Right
        else None

    for side, view in views do
        let list = FilePanel.listView view
        let cursor () = list.SelectedItem.GetValueOrDefault 0

        list.FrameChanged.Add(fun _ -> store.Refresh())

        list.ValueChanged.Add(fun args ->
            if not rendering then
                dispatch (Update.CursorMoved(side, args.NewValue.GetValueOrDefault 0)))

        view.HasFocusChanged.Add(fun _ ->
            if view.HasFocus then
                dispatch (Update.PanelFocused side))

        list.Accepting.Add(fun args ->
            args.Handled <- true
            dispatch (Update.EntryOpened(side, cursor ())))

        list.KeyDown.Add(fun key ->
            if key = refreshKey then
                key.Handled <- true
                dispatch (Update.RefreshRequested(side, cursor ()))
            elif selectionKeys |> List.contains key then
                key.Handled <- true
                dispatch (Update.SelectionToggled(side, cursor ()))
            elif key = selectPatternKey || key = deselectPatternKey then
                key.Handled <- true
                dispatch (Update.PatternRequested(side, cursor (), key = selectPatternKey))
            else
                match driveKeySide key with
                | Some target ->
                    key.Handled <- true
                    dispatch (Update.DriveRequested target)
                | None -> ())

    window
