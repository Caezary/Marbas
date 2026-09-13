module Marbas.Ui.Dialogs.DriveDialog

open System.Collections.ObjectModel
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui.Theme

[<Literal>]
let Title = "Drives"

[<Literal>]
let private NarrowestWidth = 40

[<Literal>]
let private TallestList = 12

[<Literal>]
let private FrameWidth = 4

[<Literal>]
let private FrameHeight = 5

let create (lines: string list) =
    let longest = lines |> List.map String.length |> List.fold max 0
    let width = max NarrowestWidth (longest + FrameWidth)
    let listHeight = lines.Length |> max 1 |> min TallestList

    let dialog =
        new Dialog(Title = Title, Width = Dim.Absolute width, Height = Dim.Absolute(listHeight + FrameHeight))
        |> DialogStyle.dress

    let drives =
        new ListView(X = 1, Y = 1, Width = Dim.Fill 1, Height = Dim.Absolute listHeight, KeystrokeNavigator = null)

    drives.SetSource(ObservableCollection lines)
    drives.SelectedItem <- 0

    dialog.Add drives |> ignore
    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let drives (dialog: Dialog) =
    dialog.SubViews
    |> Seq.pick (function
        | :? ListView as list -> Some list
        | _ -> None)

let choice (dialog: Dialog) =
    if dialog.Canceled then
        None
    else
        Some (drives dialog).SelectedItem.Value
