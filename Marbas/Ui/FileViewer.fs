module Marbas.Ui.FileViewer

open System
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views

[<Literal>]
let ContentId = "viewerContent"

let closeKeys = [ Key.F3; Key.Esc ]

let create (close: Dialog -> unit) title (lines: string list) =
    let dialog = new Dialog(Title = title, Width = Dim.Fill(), Height = Dim.Fill())

    let content =
        new TextView(
            Id = ContentId,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = false,
            Text = String.concat Environment.NewLine lines
        )

    content.KeyDown.Add(fun key ->
        if closeKeys |> List.contains key then
            key.Handled <- true
            close dialog)

    dialog.Add content |> ignore
    dialog

let content (dialog: Dialog) =
    dialog.SubViews
    |> Seq.pick (function
        | :? TextView as view -> Some view
        | _ -> None)
