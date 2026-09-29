module Marbas.Ui.Dialogs.PromptDialog

open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Ui.Theme

let private around title label (input: TextField) =
    let dialog =
        new Dialog(Title = title, Width = 50, Height = 6) |> DialogStyle.dress

    input.X <- 1
    input.Y <- 1
    input.Width <- Dim.Fill 1
    input.Height <- 1

    dialog.Add(new Label(X = 1, Y = 0, Text = label), input) |> ignore

    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let create title label (suggestion: string) =
    let input = new TextField(Text = suggestion)
    input.InsertionPoint <- suggestion.Length
    around title label input

let offering (remembered: string list) title label (suggestion: string) =
    around title label (HistoryField.create remembered suggestion)

let field (dialog: Dialog) =
    SubView.byType<TextField> dialog

let selecting (dialog: Dialog) =
    (field dialog).SelectAll() |> ignore
    dialog

let secret (dialog: Dialog) =
    (field dialog).Secret <- true
    dialog

let text (dialog: Dialog) =
    if dialog.Canceled then None else Some (field dialog).Text

let password (dialog: Dialog) =
    text dialog |> Option.bind ArchivePassword.tryCreate
