module Marbas.Ui.Help.HelpColours

open Terminal.Gui.Drawing
open Marbas.Guide
open Marbas.Ui.Theme

let private lookColour look =
    match look with
    | Plain -> ClassicScheme.helpText
    | Literal -> ClassicScheme.helpCode
    | Title _ -> ClassicScheme.helpTitle
    | Ruling -> ClassicScheme.helpRuling
    | Framing Note -> ClassicScheme.helpNote
    | Framing Warning -> ClassicScheme.helpWarning

let private colourOf (chosen: int option) (piece: Piece) =
    match piece.Link with
    | Some stop when Some stop.Index = chosen -> ClassicScheme.chosenLink
    | Some _ -> ClassicScheme.helpLink
    | None -> lookColour piece.Look

let private styleOf stress =
    match stress with
    | Bold -> TextStyle.Bold
    | Italic -> TextStyle.Italic

let attributeOf (chosen: int option) (piece: Piece) =
    let colour = colourOf chosen piece

    let style =
        piece.Stress
        |> Set.fold (fun style stress -> style ||| styleOf stress) colour.Style

    ClassicScheme.withStyle colour style
