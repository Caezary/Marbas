module Marbas.Ui.Dialogs.TabDialog

open System.Collections.ObjectModel
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui
open Marbas.Ui.Theme

[<Literal>]
let Title = "Tabs"

[<Literal>]
let private ListHeight = 10

let create (rows: string list) (current: int) =
    let dialog = new Dialog(Title = Title, Width = 76, Height = 15) |> DialogStyle.dress

    let tabs =
        new ListView(X = 1, Y = 1, Width = Dim.Fill 1, Height = ListHeight, KeystrokeNavigator = null)

    tabs.SetSource(ObservableCollection rows)
    tabs.SelectedItem <- current

    dialog.Add tabs |> ignore
    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let tabs (dialog: Dialog) = SubView.byType<ListView> dialog

let choice (dialog: Dialog) =
    if dialog.Canceled then
        None
    else
        (tabs dialog).SelectedItem |> Option.ofNullable
