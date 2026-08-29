module Marbas.Ui.Dialogs.PromptDialog

open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui.Theme

let create title label (suggestion: string) =
    let dialog =
        new Dialog(Title = title, Width = 50, Height = 6) |> DialogStyle.dress

    let input =
        new TextField(X = 1, Y = 1, Width = Dim.Fill 1, Height = 1, Text = suggestion)

    input.InsertionPoint <- suggestion.Length

    dialog.Add(new Label(X = 1, Y = 0, Text = label), input) |> ignore

    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let field (dialog: Dialog) =
    dialog.SubViews
    |> Seq.pick (function
        | :? TextField as input -> Some input
        | _ -> None)

let selecting (dialog: Dialog) =
    (field dialog).SelectAll() |> ignore
    dialog

let secret (dialog: Dialog) =
    (field dialog).Secret <- true
    dialog

let text (dialog: Dialog) =
    if dialog.Canceled then None else Some (field dialog).Text
