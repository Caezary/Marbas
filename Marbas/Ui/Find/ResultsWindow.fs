module Marbas.Ui.Find.ResultsWindow

open System.Collections.ObjectModel
open System.IO.Abstractions
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Find
open Marbas.Find.FileSearch
open Marbas.Ui
open Marbas.Ui.Theme
open Marbas.Ui.Format
open Marbas.Ui.Keys

[<Literal>]
let ListId = "resultsList"

[<Literal>]
let SummaryId = "resultsSummary"

[<Literal>]
let KeyBarId = "resultsKeyBar"

type ResultsView(fs: IFileSystem, run: SearchRun) =
    inherit Window(Width = Dim.Fill(), Height = Dim.Fill())

    let mutable state = ResultsState.start run

    member _.FileSystem = fs

    member _.State = state

    member _.Change(change: ResultsState -> ResultsState) = state <- change state

    member _.Step(step: ResultsState -> 'answer * ResultsState) =
        let answer, next = step state
        state <- next
        answer

let list (window: ResultsView) = SubView.byId<ListView> ListId window

let summary (window: ResultsView) = SubView.byId<View> SummaryId window

let keyBar (window: ResultsView) = SubView.byId<View> KeyBarId window

let private shownLines (window: ResultsView) =
    match (list window).Source with
    | null -> None
    | source -> Some(source.ToList() :?> ObservableCollection<string>)

let private cursor (window: ResultsView) =
    (list window).SelectedItem |> Option.ofNullable |> Option.defaultValue 0

let private updateSummary (window: ResultsView) =
    (summary window).Text <- ResultsState.progress window.FileSystem window.State |> FindSummary.describe

let private repaint (window: ResultsView) =
    let view = list window
    let standing = cursor window
    window.Change(ResultsState.laidOut (max 1 view.Viewport.Width))
    let lines = ObservableCollection(ResultsState.lines window.FileSystem window.State)
    view.SetSource lines

    if lines.Count > 0 then
        view.SelectedItem <- max 0 (min standing (lines.Count - 1))

    updateSummary window
    view.SetNeedsDraw()

let private viewRow (window: ResultsView) (view: FindHit -> unit) =
    ResultsState.viewableAt (cursor window) window.State |> Option.iter view

let private landOn (window: ResultsView) (close: Window -> unit) landing =
    window.Change(ResultsState.landed landing)
    close window

let private standOn (window: ResultsView) (close: Window -> unit) =
    ResultsState.landingAt (cursor window) window.State
    |> Option.iter (landOn window close)

let private sendToPanel (window: ResultsView) (close: Window -> unit) = landOn window close InPanel

let answer (window: ResultsView) =
    ResultsState.answer (cursor window) window.State

let private leave (window: ResultsView) (stop: unit -> unit) (close: Window -> unit) =
    match window.Step ResultsState.leaving with
    | StopsWalk -> stop ()
    | ClosesWindow -> close window

let private standAt (hits: ListView) cursor =
    match hits.Source with
    | null -> ()
    | source -> hits.SelectedItem <- max 0 (min cursor (source.Count - 1))

let create (fs: IFileSystem) (run: SearchRun) (view: FindHit -> unit) (stop: unit -> unit) (close: Window -> unit) =
    let window = new ResultsView(fs, run)

    window.BorderStyle <- LineStyle.None
    window.SetScheme ClassicScheme.panel |> ignore

    let hits =
        new ListView(Id = ListId, X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill 2, KeystrokeNavigator = null)

    hits.SetSource(ObservableCollection<string>())

    let line =
        new Label(Id = SummaryId, X = 0, Y = Pos.AnchorEnd 2, Width = Dim.Fill(), Height = 1)
        |> LiteralText.showing

    line.SetScheme ClassicScheme.summary |> ignore

    let bar =
        KeyMap.functionKeys
        |> List.mapi (fun index _ -> $"F{index + 1}")
        |> FunctionKeyBar.build KeyBarId

    hits.KeyDown.Add(fun (key: Key) ->
        if FindKeys.closes key then
            key.Handled <- true
            leave window stop close
        elif key = FindKeys.viewKey then
            key.Handled <- true
            viewRow window view
        elif key = FindKeys.standKey then
            key.Handled <- true
            standOn window close
        elif key = FindKeys.panelKey then
            key.Handled <- true
            sendToPanel window close)

    window.Add(hits, line, bar)
    FunctionKeyBar.label bar FindKeys.bar

    window.SubViewsLaidOut.Add(fun _ ->
        if (list window).Viewport.Width <> ResultsState.room window.State then
            repaint window)

    hits.SetFocus() |> ignore
    repaint window

    match run with
    | Walk _ -> ()
    | Reopen(results, _) -> standAt hits results.Cursor

    window

let add (window: ResultsView) (reports: FindReport list) =
    let room = ResultsState.room window.State
    let added = window.Step(ResultsState.arrived reports)

    shownLines window
    |> Option.iter (fun lines ->
        for row in added do
            lines.Add(ResultRow.describe window.FileSystem room row))

    updateSummary window
    (list window).SetNeedsDraw()

let walking (window: ResultsView) (directory: string) =
    window.Change(ResultsState.walking directory)
    updateSummary window

let finish (window: ResultsView) (outcome: FindOutcome) =
    window.Change(ResultsState.ended outcome)
    updateSummary window
