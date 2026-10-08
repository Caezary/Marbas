module Marbas.Ui.Dialogs.KeepFavoriteDialog

open System.IO.Abstractions
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Ui.Format
open Marbas.Ui.Keys
open Marbas.Ui.Theme

[<Literal>]
let Title = "Add to favourites"

[<Literal>]
let private QuestionLead = "Press a digit to add "

[<Literal>]
let private QuestionTail = " to favourites"

[<Literal>]
let private FrameWidth = 4

[<Literal>]
let private FrameHeight = 5

type KeepAnswer =
    | Waiting
    | Answered of FavoriteSlot option

type KeepFavoriteView(width: int) =
    inherit Dialog(Title = Title, Width = Dim.Absolute width, Height = Dim.Absolute FrameHeight)

    member val Answer = Waiting with get, set

let private answer (dialog: KeepFavoriteView) (key: Key) =
    key.Handled <- true

    match dialog.Answer with
    | Waiting ->
        dialog.Answer <- Answered(KeyMap.favoriteSlotOf key)
        dialog.RequestStop()
    | Answered _ -> ()

let private asking (fs: IFileSystem) screenWidth place =
    let room = screenWidth - FrameWidth - QuestionLead.Length - QuestionTail.Length

    let path =
        HistoryRow.place fs place
        |> PathText.shorten fs.Path.DirectorySeparatorChar room

    QuestionLead + path + QuestionTail

let create (fs: IFileSystem) (screenWidth: int) (place: Place) =
    let text = asking fs screenWidth place
    let dialog = new KeepFavoriteView(text.Length + FrameWidth)
    DialogStyle.dress dialog |> ignore

    new Label(X = 1, Y = 1, Height = 1, Text = text)
    |> LiteralText.showing
    |> dialog.Add
    |> ignore

    dialog.KeyDown.Add(answer dialog)
    dialog

let question (dialog: KeepFavoriteView) = SubView.byType<Label> dialog

let slot (dialog: KeepFavoriteView) =
    match dialog.Answer with
    | Answered chosen -> chosen
    | Waiting -> None
