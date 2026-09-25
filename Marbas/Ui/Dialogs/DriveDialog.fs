module Marbas.Ui.Dialogs.DriveDialog

open System
open System.Collections.ObjectModel
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Ui.Keys
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

[<Literal>]
let private AcceptedButton = 1

type DriveView(rows: DriveRow list, width: int, listHeight: int) =
    inherit Dialog(Title = Title, Width = Dim.Absolute width, Height = Dim.Absolute(listHeight + FrameHeight))

    member val HandingOver = false with get, set

    member _.Rows = rows

let private handOver (dialog: DriveView) (key: Key) =
    if key = KeyMap.openWithKey then
        key.Handled <- true
        dialog.HandingOver <- true
        dialog.Result <- Nullable AcceptedButton
        dialog.RequestStop()

let create (rows: DriveRow list) =
    let lines = DriveLine.describe rows
    let longest = lines |> List.map String.length |> List.fold max 0
    let width = max NarrowestWidth (longest + FrameWidth)
    let listHeight = lines.Length |> max 1 |> min TallestList

    let dialog = new DriveView(rows, width, listHeight)
    DialogStyle.dress dialog |> ignore

    let drives =
        new ListView(X = 1, Y = 1, Width = Dim.Fill 1, Height = Dim.Absolute listHeight, KeystrokeNavigator = null)

    drives.SetSource(ObservableCollection lines)
    drives.SelectedItem <- 0

    dialog.KeyDown.Add(handOver dialog)
    dialog.Add drives |> ignore
    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let drives (dialog: Dialog) =
    SubView.byType<ListView> dialog

let choice (dialog: DriveView) =
    let row () = dialog.Rows[(drives dialog).SelectedItem.Value]

    match dialog.Canceled, dialog.HandingOver with
    | true, _ -> None
    | false, true -> Some(HandOver(row ()))
    | false, false -> Some(StandOn(row ()))
