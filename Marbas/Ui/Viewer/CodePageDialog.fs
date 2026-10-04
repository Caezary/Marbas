module Marbas.Ui.Viewer.CodePageDialog

open System.Collections.ObjectModel
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui
open Marbas.Ui.Theme

[<Literal>]
let Title = "Code page"

[<Literal>]
let private Width = 30

[<Literal>]
let private FrameHeight = 5

let create (offer: CodePageOffer) =
    let rows = CodePage.offered |> List.map (CodePage.label offer.Detected)

    let dialog =
        new Dialog(Title = Title, Width = Dim.Absolute Width, Height = Dim.Absolute(rows.Length + FrameHeight))
        |> DialogStyle.dress

    let pages =
        new ListView(X = 1, Y = 1, Width = Dim.Fill 1, Height = Dim.Absolute rows.Length, KeystrokeNavigator = null)

    pages.SetSource(ObservableCollection rows)

    pages.SelectedItem <-
        CodePage.offered
        |> List.tryFindIndex ((=) offer.Current)
        |> Option.defaultValue 0

    dialog.Add pages |> ignore
    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let pages (dialog: Dialog) = SubView.byType<ListView> dialog

let choice (dialog: Dialog) =
    if dialog.Canceled then
        None
    else
        (pages dialog).SelectedItem
        |> Option.ofNullable
        |> Option.bind (fun index -> List.tryItem index CodePage.offered)
