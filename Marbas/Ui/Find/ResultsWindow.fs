module Marbas.Ui.Find.ResultsWindow

open System.Collections.ObjectModel
open System.IO.Abstractions
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.FileSystem.FileSearch
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

let private nothing =
    { Files = 0
      Directories = 0
      Skipped = 0 }

let private countsOf (hits: FindHit seq) =
    let kind wanted =
        hits |> Seq.filter (fun hit -> hit.Entry.Kind = wanted) |> Seq.length

    { Files = kind DirectoryListing.File
      Directories = kind DirectoryListing.Directory
      Skipped = 0 }

type ResultsView(fs: IFileSystem, run: SearchRun) =
    inherit Window(Width = Dim.Fill(), Height = Dim.Fill())

    let found = ResizeArray<FindHit>()
    let shown = ResizeArray<ResultRow.ResultRow>()
    let mutable lastLocation: PanelLocation option = None
    let mutable counts = nothing
    let mutable directory = ""
    let mutable ended: FindOutcome option = None
    let mutable asked = false

    let reopened =
        match run with
        | Walk _ -> None
        | Reopen(results, gone) -> Some(results, gone)

    let lastLocationAfter (hits: FindHit list) =
        hits |> List.tryLast |> Option.map _.Location |> Option.orElse lastLocation

    do
        match run with
        | Walk _ -> ()
        | Reopen(results, _) ->
            found.AddRange results.Hits
            shown.AddRange(ResultRow.following None results.Hits)
            lastLocation <- lastLocationAfter results.Hits
            counts <- countsOf results.Hits
            ended <- Some results.Outcome

    member val Room = 0 with get, set

    member val Lines = ObservableCollection<string>() with get, set

    member val Landing: FindLanding option = None with get, set

    member _.Hits = List.ofSeq found

    member _.Rows = List.ofSeq shown

    member _.RowAt index =
        if index >= 0 && index < shown.Count then
            Some shown[index]
        else
            None

    member _.Running = Option.isNone ended

    member _.Asked = asked

    member _.Ask() = asked <- true

    member _.Arrived(reports: FindReport list) =
        let hits = reports |> List.map _.Hit
        let rows = ResultRow.following lastLocation hits

        found.AddRange hits
        shown.AddRange rows
        lastLocation <- lastLocationAfter hits
        reports |> List.tryLast |> Option.iter (fun report -> counts <- report.Counts)

        rows

    member _.Progress =
        match reopened, ended with
        | Some(results, gone), _ ->
            FindSummary.Reopened(QueryText.describe results.Query, QueryText.where fs results.Ground, counts, gone)
        | None, Some outcome -> FindSummary.Done outcome
        | None, None -> FindSummary.Walking(directory, counts)

    member _.Ran = run

    member _.Outcome = ended |> Option.defaultWith (fun () -> Stopped counts)

    member _.Walking(place: string) = directory <- place

    member _.Ended(outcome: FindOutcome) = ended <- Some outcome

    member _.Describe width row = ResultRow.describe fs width row

let list (window: ResultsView) = SubView.byId<ListView> ListId window

let summary (window: ResultsView) = SubView.byId<View> SummaryId window

let keyBar (window: ResultsView) = SubView.byId<View> KeyBarId window

let private updateSummary (window: ResultsView) =
    (summary window).Text <- FindSummary.describe window.Progress

let private repaint (window: ResultsView) =
    let view = list window
    let standing = view.SelectedItem |> Option.ofNullable |> Option.defaultValue 0
    window.Room <- max 1 view.Viewport.Width
    window.Lines <- ObservableCollection(window.Rows |> List.map (window.Describe window.Room))
    view.SetSource window.Lines

    if window.Lines.Count > 0 then
        view.SelectedItem <- max 0 (min standing (window.Lines.Count - 1))

    updateSummary window
    view.SetNeedsDraw()

let private rowUnderCursor (window: ResultsView) =
    let standing =
        (list window).SelectedItem |> Option.ofNullable |> Option.defaultValue 0

    window.RowAt standing

let private viewRow (window: ResultsView) (view: FindHit -> unit) =
    match rowUnderCursor window with
    | Some(ResultRow.Hit hit) when hit.Entry.Kind = DirectoryListing.File -> view hit
    | _ -> ()

let private standOn (window: ResultsView) (close: Window -> unit) =
    match rowUnderCursor window with
    | None -> ()
    | Some row ->
        window.Landing <-
            match row with
            | ResultRow.Hit hit -> Some(AtHit hit)
            | ResultRow.GroupHeader location -> Some(At(Into location))

        close window

let private sendToPanel (window: ResultsView) (close: Window -> unit) =
    window.Landing <- Some InPanel
    close window

let answer (window: ResultsView) =
    { Landing = window.Landing
      Results =
        { Query = SearchRun.query window.Ran
          Ground = SearchRun.ground window.Ran
          Hits = window.Hits
          Outcome = window.Outcome
          Cursor = (list window).SelectedItem |> Option.ofNullable |> Option.defaultValue 0 } }

let private leave (window: ResultsView) (stop: unit -> unit) (close: Window -> unit) =
    if window.Running && not window.Asked then
        window.Ask()
        stop ()
    else
        close window

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
        if (list window).Viewport.Width <> window.Room then
            repaint window)

    hits.SetFocus() |> ignore
    repaint window

    match run with
    | Walk _ -> ()
    | Reopen(results, _) -> hits.SelectedItem <- max 0 (min results.Cursor (hits.Source.Count - 1))

    window

let add (window: ResultsView) (reports: FindReport list) =
    for row in window.Arrived reports do
        window.Lines.Add(window.Describe window.Room row)

    updateSummary window
    (list window).SetNeedsDraw()

let walking (window: ResultsView) (directory: string) =
    window.Walking directory
    updateSummary window

let finish (window: ResultsView) (outcome: FindOutcome) =
    window.Ended outcome
    updateSummary window
