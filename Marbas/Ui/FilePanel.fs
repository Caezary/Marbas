module Marbas.Ui.FilePanel

open Terminal.Gui.Drawing
open Terminal.Gui.ViewBase
open Terminal.Gui.Views

[<Literal>]
let LeftId = "leftPanel"

[<Literal>]
let RightId = "rightPanel"

let private focusable (panel: FrameView) =
    panel.CanFocus <- true
    panel.TabStop <- TabBehavior.TabStop
    panel.BorderStyle <- LineStyle.Single

    panel.HasFocusChanged.Add(fun _ ->
        panel.BorderStyle <- if panel.HasFocus then LineStyle.Double else LineStyle.Single)

    panel

let createPair () =
    let left =
        new FrameView(Id = LeftId, Width = Dim.Percent 50, Height = Dim.Fill())

    let right =
        new FrameView(Id = RightId, X = Pos.Right left, Width = Dim.Fill(), Height = Dim.Fill())

    focusable left, focusable right
