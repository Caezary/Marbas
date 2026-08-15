module Marbas.Ui.FilePanel

open System.Collections.ObjectModel
open Terminal.Gui.Drawing
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.State

[<Literal>]
let LeftId = "leftPanel"

[<Literal>]
let RightId = "rightPanel"

let private focusable (panel: FrameView) =
    panel.CanFocus <- true
    panel.TabStop <- TabBehavior.TabStop
    panel.BorderStyle <- LineStyle.Single
    panel.SetScheme ClassicScheme.panel |> ignore

    panel.HasFocusChanged.Add(fun _ ->
        panel.BorderStyle <- if panel.HasFocus then LineStyle.Double else LineStyle.Single)

    let list =
        new ListView(Width = Dim.Fill(), Height = Dim.Fill 1, MarkMultiple = true)

    list.KeystrokeNavigator <- null

    let summary =
        new Label(Y = Pos.AnchorEnd 1, Width = Dim.Fill(), Height = 1)

    summary.SetScheme ClassicScheme.summary |> ignore

    panel.Add(list, summary) |> ignore

    panel

let listView (panel: View) =
    panel.SubViews
    |> Seq.pick (function
        | :? ListView as list -> Some list
        | _ -> None)

let summary (panel: View) =
    panel.SubViews
    |> Seq.pick (function
        | :? Label as label -> Some label
        | _ -> None)

let render (state: AppState.PanelState) (panel: FrameView) =
    panel.Title <- FileSystemPath.value state.Path
    let list = listView panel
    let width = list.Viewport.Width

    let rows =
        if width > 0 then
            state.Entries |> List.map (EntryFormat.format width)
        else
            state.Entries |> List.map (fun e -> EntryName.value e.Name)

    list.SetSource(ObservableCollection rows)

    state.Entries
    |> List.iteri (fun index entry -> list.Source.SetMark(index, state.Selected.Contains entry.Name))

    if not rows.IsEmpty then
        list.SelectedItem <- min state.Cursor (rows.Length - 1)

    (summary panel).Text <- SelectionSummary.describe (AppState.selectedEntries state)

type Panels = { Left: FrameView; Right: FrameView }

let pairs (panels: Panels) =
    [ AppState.Left, panels.Left; AppState.Right, panels.Right ]

let createPair () =
    let left =
        new FrameView(Id = LeftId, Width = Dim.Percent 50, Height = Dim.Fill 2)

    let right =
        new FrameView(Id = RightId, X = Pos.Right left, Width = Dim.Fill(), Height = Dim.Fill 2)

    { Left = focusable left
      Right = focusable right }
