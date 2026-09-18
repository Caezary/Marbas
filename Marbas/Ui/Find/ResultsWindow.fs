module Marbas.Ui.Find.ResultsWindow

open System.Collections.ObjectModel
open System.IO.Abstractions
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.FileSystem.FileSearch
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

type ResultsView(fs: IFileSystem, run: SearchRun) =
    inherit Window(Width = Dim.Fill(), Height = Dim.Fill())

    let found = ResizeArray<FindHit>()
    let mutable directory = ""
    let mutable ended: FindOutcome option = None
    let mutable asked = false

    let reopened =
        match run with
        | Walk _ -> None
        | Reopen(results, gone) -> Some(results, gone)

    do
        match run with
        | Walk _ -> ()
        | Reopen(results, _) ->
            found.AddRange results.Hits
            ended <- Some results.Outcome

    member val Room = 0 with get, set

    member val Landing: FindLanding option = None with get, set

    member _.Hits = List.ofSeq found

    member _.Rows = ResultRow.rows (List.ofSeq found)

    member _.Running = Option.isNone ended

    member _.Asked = asked

    member _.Ask() = asked <- true

    member _.Gather(hits: FindHit list) = found.AddRange hits

    member _.Counted =
        let kind wanted =
            found |> Seq.filter (fun hit -> hit.Entry.Kind = wanted) |> Seq.length

        { Files = kind DirectoryListing.File
          Directories = kind DirectoryListing.Directory
          Skipped = 0 }

    member this.Progress =
        match reopened, ended with
        | Some(results, gone), _ ->
            FindSummary.Reopened(QueryText.describe results.Query, QueryText.where fs results.Ground, this.Counted, gone)
        | None, Some outcome -> FindSummary.Done outcome
        | None, None -> FindSummary.Walking(directory, this.Counted)

    member _.Ran = run

    member this.Outcome =
        ended |> Option.defaultWith (fun () -> Stopped this.Counted)

    member _.Walking(place: string) = directory <- place

    member _.Ended(outcome: FindOutcome) = ended <- Some outcome

    member _.Describe width row = ResultRow.describe fs width row

let list (window: ResultsView) =
    window.SubViews
    |> Seq.pick (function
        | :? ListView as view when view.Id = ListId -> Some view
        | _ -> None)

let summary (window: ResultsView) =
    window.SubViews |> Seq.find (fun view -> view.Id = SummaryId)

let keyBar (window: ResultsView) =
    window.SubViews |> Seq.find (fun view -> view.Id = KeyBarId)

let private standingRow (view: ListView) =
    let standing = view.SelectedItem |> Option.ofNullable |> Option.defaultValue 0

    view.Source
    |> Option.ofObj
    |> Option.map _.ToList()
    |> Option.filter (fun rows -> standing >= 0 && standing < rows.Count)
    |> Option.map (fun rows -> rows[standing].ToString())

let private refresh (window: ResultsView) =
    let view = list window
    let standing = view.SelectedItem |> Option.ofNullable |> Option.defaultValue 0
    let carried = standingRow view
    window.Room <- max 1 view.Viewport.Width
    let shown = window.Rows |> List.map (window.Describe window.Room)

    let landing =
        carried
        |> Option.bind (fun row -> shown |> List.tryFindIndex ((=) row))
        |> Option.defaultValue standing

    view.SetSource(ObservableCollection shown)

    if not (List.isEmpty shown) then
        view.SelectedItem <- max 0 (min landing (List.length shown - 1))

    (summary window).Text <- FindSummary.describe window.Progress
    view.SetNeedsDraw()

let private rowUnderCursor (window: ResultsView) =
    let standing = (list window).SelectedItem |> Option.ofNullable |> Option.defaultValue 0

    window.Rows |> List.tryItem standing

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
            | ResultRow.GroupHeader location -> Some(AtDirectory location)

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
            standOn window close)

    window.Add(hits, line, bar)
    FunctionKeyBar.label bar FindKeys.bar

    window.SubViewsLaidOut.Add(fun _ ->
        if (list window).Viewport.Width <> window.Room then
            refresh window)

    hits.SetFocus() |> ignore
    refresh window

    match run with
    | Walk _ -> ()
    | Reopen(results, _) -> hits.SelectedItem <- max 0 (min results.Cursor (hits.Source.Count - 1))

    window

let add (window: ResultsView) (hits: FindHit list) =
    window.Gather hits
    refresh window

let walking (window: ResultsView) (directory: string) =
    window.Walking directory
    refresh window

let finish (window: ResultsView) (outcome: FindOutcome) =
    window.Ended outcome
    refresh window
