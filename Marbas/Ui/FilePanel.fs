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

type PanelView() =
    inherit FrameView()

    member val RowColors: Attribute array = Array.empty with get, set

let listView (panel: View) =
    panel.SubViews
    |> Seq.pick (function
        | :? ListView as list -> Some list
        | _ -> None)

let footer (panel: View) =
    panel.SubViews
    |> Seq.pick (function
        | :? Label as label -> Some label
        | _ -> None)

let private rowColor (panel: PanelView) row =
    panel.RowColors |> Array.tryItem row

let private paintRows (panel: PanelView) (list: ListView) =
    list.RowRender.Add(fun args ->
        let marked = list.Source <> null && list.Source.IsMarked args.Row
        let underCursor = list.HasFocus && list.SelectedItem.GetValueOrDefault -1 = args.Row

        if not marked && not underCursor then
            match rowColor panel args.Row with
            | Some color -> args.RowAttribute <- color
            | None -> ())

let private focusable (panel: PanelView) =
    panel.CanFocus <- true
    panel.TabStop <- TabBehavior.TabStop
    panel.BorderStyle <- LineStyle.Single
    panel.SetScheme ClassicScheme.panel |> ignore

    panel.HasFocusChanged.Add(fun _ ->
        panel.BorderStyle <- if panel.HasFocus then LineStyle.Double else LineStyle.Single)

    let list =
        new ListView(Width = Dim.Fill(), Height = Dim.Fill 2, MarkMultiple = true)

    list.KeystrokeNavigator <- null
    paintRows panel list

    let rule =
        new Line(Y = Pos.AnchorEnd 2, Width = Dim.Fill(), Orientation = Orientation.Horizontal)

    let footer =
        new Label(Y = Pos.AnchorEnd 1, Width = Dim.Fill(), Height = 1)
        |> LiteralText.showing

    footer.SetScheme ClassicScheme.summary |> ignore

    panel.Add(list, rule, footer) |> ignore

    panel

let render (state: AppState.PanelState) (panel: PanelView) =
    panel.Title <- PanelTitle.describe state.Path state.Sort state.Hidden
    let list = listView panel
    let width = list.Viewport.Width

    let rows =
        if width > 0 then
            state.Entries |> List.map (EntryFormat.format width)
        else
            state.Entries |> List.map (fun e -> EntryName.value e.Name)

    panel.RowColors <-
        state.Entries
        |> List.map (EntryColor.classify >> EntryColor.attribute)
        |> Array.ofList

    list.SetSource(ObservableCollection rows)

    state.Entries
    |> List.iteri (fun index entry -> list.Source.SetMark(index, state.Selected.Contains entry.Name))

    if not rows.IsEmpty then
        list.SelectedItem <- min state.Cursor (rows.Length - 1)

    (footer panel).Text <- PanelFooter.describe state

type Panels = { Left: PanelView; Right: PanelView }

let pairs (panels: Panels) =
    [ AppState.Left, panels.Left; AppState.Right, panels.Right ]

let createPair () =
    let left =
        new PanelView(Id = LeftId, Width = Dim.Percent 50, Height = Dim.Fill 2)
        |> LiteralText.showing

    let right =
        new PanelView(Id = RightId, X = Pos.Right left, Width = Dim.Fill(), Height = Dim.Fill 2)
        |> LiteralText.showing

    { Left = focusable left
      Right = focusable right }
