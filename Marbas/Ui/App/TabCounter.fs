module Marbas.Ui.App.TabCounter

open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Collections
open Marbas.State
open Marbas.Ui.Format

[<Literal>]
let Id = "tabCounter"

let describe (state: AppState.AppState) =
    $"[{Zipper.position state.Tabs + 1}/{Zipper.count state.Tabs}]"

let create () =
    new Label(Id = Id, Y = 0, Height = 1) |> LiteralText.showing

let render (state: AppState.AppState) (counter: Label) =
    let text = describe state
    counter.Text <- text
    counter.Width <- Dim.Absolute text.Length
    counter.X <- Pos.AnchorEnd(text.Length + 1)
