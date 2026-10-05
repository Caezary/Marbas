module Marbas.Ui.Dialogs.ViewerSearchDialog

open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Ui.Theme
open Marbas.Ui.Viewer

[<Literal>]
let Title = "Search"

[<Literal>]
let TextId = "viewerSearchText"

[<Literal>]
let private SecondColumn = 26

[<Literal>]
let private Width = 54

[<Literal>]
let private Height = 9

type SearchBox =
    | CaseBox
    | WordsBox
    | ExpressionBox
    | BackwardsBox

let boxLabel box =
    match box with
    | CaseBox -> "C_ase sensitive"
    | WordsBox -> "_Whole words"
    | ExpressionBox -> "_Regular expression"
    | BackwardsBox -> "_Backwards"

let boxId box =
    match box with
    | CaseBox -> "viewerSearchCase"
    | WordsBox -> "viewerSearchWords"
    | ExpressionBox -> "viewerSearchExpression"
    | BackwardsBox -> "viewerSearchBackwards"

let private placement =
    [ CaseBox, 1, 3
      WordsBox, SecondColumn, 3
      ExpressionBox, 1, 4
      BackwardsBox, SecondColumn, 4 ]

let boxes = placement |> List.map (fun (box, _, _) -> box)

let private waitsForItsTask box =
    match box with
    | ExpressionBox -> true
    | CaseBox
    | WordsBox
    | BackwardsBox -> false

let private tickedFor (last: WindowSearch.Query option) box =
    let ticked =
        match box, last with
        | _, None -> false
        | CaseBox, Some query -> query.Case = MatchingCase
        | WordsBox, Some query -> query.Words = WholeWords
        | BackwardsBox, Some query -> query.Direction = Backwards
        | ExpressionBox, Some _ -> false

    if ticked then CheckState.Checked else CheckState.UnChecked

let private checking (last: WindowSearch.Query option) (box, x, y) =
    new CheckBox(
        X = Pos.Absolute x,
        Y = Pos.Absolute y,
        Height = 1,
        Id = boxId box,
        Text = boxLabel box,
        Enabled = not (waitsForItsTask box),
        Value = tickedFor last box
    )

let private input (offer: SearchOffer) =
    let last =
        offer.Last
        |> Option.map (_.Text >> FindText.value)
        |> Option.defaultValue ""
    let field = HistoryField.amongOthers (offer.Remembered |> List.map EnteredText.value) last
    field.X <- 1
    field.Y <- 1
    field.Width <- Dim.Fill 1
    field.Height <- 1
    field.Id <- TextId
    field.SelectAll() |> ignore
    DialogStyle.typedInto field

let create (offer: SearchOffer) =
    let dialog =
        new Dialog(Title = Title, Width = Width, Height = Height) |> DialogStyle.dress

    dialog.Add(new Label(X = 1, Y = 0, Height = 1, Text = "Search for:"), input offer)

    placement
    |> List.iter (fun placed -> dialog.Add(checking offer.Last placed) |> ignore)
    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let field (dialog: Dialog) = SubView.byId<TextField> TextId dialog

let checkBox (dialog: Dialog) box =
    SubView.byId<CheckBox> (boxId box) dialog

let private ticked dialog box =
    (checkBox dialog box).Value = CheckState.Checked

let private chosen (dialog: Dialog) =
    { Typed = (field dialog).Text
      Case = if ticked dialog CaseBox then MatchingCase else IgnoringCase
      Words = if ticked dialog WordsBox then WholeWords else Anywhere
      Direction = if ticked dialog BackwardsBox then Backwards else Forwards }

let answer (dialog: Dialog) =
    if dialog.Canceled then None else Some(chosen dialog)
