module Marbas.Ui.FilePanel

open Terminal.Gui.ViewBase
open Terminal.Gui.Views

[<Literal>]
let LeftId = "leftPanel"

[<Literal>]
let RightId = "rightPanel"

let createPair () =
    let left =
        new FrameView(Id = LeftId, Width = Dim.Percent 50, Height = Dim.Fill())

    let right =
        new FrameView(Id = RightId, X = Pos.Right left, Width = Dim.Fill(), Height = Dim.Fill())

    left, right
