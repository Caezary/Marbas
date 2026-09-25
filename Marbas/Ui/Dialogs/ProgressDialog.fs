module Marbas.Ui.Dialogs.ProgressDialog

open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui
open Marbas.Ui.Theme
open Marbas.Ui.Format

[<Literal>]
let LineId = "progressLine"

let create title =
    let dialog =
        new Dialog(Title = title, Width = 60, Height = 6) |> DialogStyle.dress

    dialog.Add(
        new Label(X = 1, Y = 0, Width = Dim.Fill 1, Height = 1, Text = "", Id = LineId)
        |> LiteralText.showing
    )
    |> ignore

    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog

let line (dialog: Dialog) =
    SubView.byId<Label> LineId dialog

let report (dialog: Dialog) (text: string) = (line dialog).Text <- text
