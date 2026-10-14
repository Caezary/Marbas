module Marbas.Ui.Panels.FilePanel

open System.Collections.ObjectModel
open System.IO.Abstractions
open Terminal.Gui.Drawing
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.State
open Marbas.Ui
open Marbas.Ui.Theme
open Marbas.Ui.Format

[<Literal>]
let LeftId = "leftPanel"

[<Literal>]
let RightId = "rightPanel"

type PanelView() =
    inherit FrameView()

    member val RowColors: Attribute array = Array.empty with get, set

    member val Painted: PanelRows.Painting option = None with get, set

let listView (panel: View) = SubView.byType<ListView> panel

let footer (panel: View) = SubView.byType<Label> panel

let private rowColor (panel: PanelView) row = panel.RowColors |> Array.tryItem row

let private rowAttribute (panel: PanelView) (list: ListView) row =
    let marked = list.Source <> null && list.Source.IsMarked row
    let underCursor = list.HasFocus && list.SelectedItem.GetValueOrDefault -1 = row

    match underCursor, marked with
    | true, _ -> None
    | false, true -> Some ClassicScheme.markedEntry
    | false, false -> rowColor panel row

let private paintRows (panel: PanelView) (list: ListView) =
    list.RowRender.Add(fun args ->
        rowAttribute panel list args.Row
        |> Option.iter (fun color -> args.RowAttribute <- color))

[<Literal>]
let private BorderThickness = 1

let private focusable (panel: PanelView) =
    panel.CanFocus <- true
    panel.TabStop <- TabBehavior.TabStop
    panel.BorderStyle <- LineStyle.Single
    panel.SetScheme ClassicScheme.panel |> ignore

    panel.HasFocusChanged.Add(fun _ ->
        panel.BorderStyle <-
            if panel.HasFocus then
                LineStyle.Double
            else
                LineStyle.Single)

    let list = new ListView(Width = Dim.Fill(), Height = Dim.Fill 2)

    list.KeystrokeNavigator <- null
    paintRows panel list

    let rule =
        new Line(
            X = Pos.Absolute -BorderThickness,
            Y = Pos.AnchorEnd 2,
            Width = Dim.Fill() + Dim.Absolute BorderThickness,
            Orientation = Orientation.Horizontal
        )

    let footer =
        new Label(Y = Pos.AnchorEnd 1, Width = Dim.Fill(), Height = 1)
        |> LiteralText.showing

    footer.SetScheme ClassicScheme.summary |> ignore

    panel.Add(list, rule, footer)

    panel

[<Literal>]
let private FrameAndCorners = 4

[<Literal>]
let private Unmeasured = System.Int32.MaxValue

let private roomForTitle (panel: PanelView) taken =
    match panel.Frame.Width with
    | 0 -> Unmeasured
    | width -> width - FrameAndCorners - taken

let private fill (panel: PanelView) (list: ListView) (painting: PanelRows.Painting) =
    let rows =
        if painting.Width > 0 then
            painting.Entries |> Array.map (EntryFormat.format painting.Width)
        else
            painting.Entries |> Array.map (fun e -> EntryName.value e.Name)

    panel.RowColors <- painting.Entries |> Array.map (EntryColor.classify >> EntryColor.attribute)

    list.SetSource(ObservableCollection rows)

    painting.Entries
    |> Array.iteri (fun index entry -> list.Source.SetMark(index, painting.Selected.Contains entry.Name))

let render (fs: IFileSystem) (taken: int) (state: PanelState) (panel: PanelView) =
    panel.Title <- PanelTitle.describe fs (roomForTitle panel taken) state.Path state.Sort state.Hidden
    let list = listView panel
    let painting = PanelRows.painting list.Viewport.Width state

    match PanelRows.rows panel.Painted painting with
    | PanelRows.Kept -> ()
    | PanelRows.Rebuilt -> fill panel list painting

    panel.Painted <- Some painting

    if painting.Entries.Length > 0 then
        list.SelectedItem <- min state.Cursor (painting.Entries.Length - 1)

    (footer panel).Text <- PanelFooter.describe state

type Panels = { Left: PanelView; Right: PanelView }

let pairs (panels: Panels) =
    [ Left, panels.Left; Right, panels.Right ]

let createPair () =
    let left =
        new PanelView(Id = LeftId, Width = Dim.Percent 50, Height = Dim.Fill 2)
        |> LiteralText.showing

    let right =
        new PanelView(Id = RightId, X = Pos.Right left, Width = Dim.Fill(), Height = Dim.Fill 2)
        |> LiteralText.showing

    { Left = focusable left
      Right = focusable right }
