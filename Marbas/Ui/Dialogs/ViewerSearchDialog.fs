module Marbas.Ui.Dialogs.ViewerSearchDialog

open Terminal.Gui.Input
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

let private tickedFor (last: WindowSearch.Query option) box =
    match box, last with
    | _, None -> false
    | CaseBox, Some query -> query.Case = MatchingCase
    | WordsBox, Some { Wanted = WindowSearch.Literal(_, words) } -> words = WholeWords
    | WordsBox, Some { Wanted = WindowSearch.Pattern _ } -> false
    | ExpressionBox, Some { Wanted = WindowSearch.Pattern _ } -> true
    | ExpressionBox, Some { Wanted = WindowSearch.Literal _ } -> false
    | BackwardsBox, Some query -> query.Direction = Backwards

let private stateOf ticked =
    if ticked then CheckState.Checked else CheckState.UnChecked

let private checking (last: WindowSearch.Query option) (box, x, y) =
    new CheckBox(
        X = Pos.Absolute x,
        Y = Pos.Absolute y,
        Height = 1,
        Id = boxId box,
        Text = boxLabel box,
        Value = stateOf (tickedFor last box)
    )

let private input (offer: SearchOffer) =
    let last =
        offer.Last
        |> Option.map (_.Wanted >> WindowSearch.spelled)
        |> Option.defaultValue ""
    let field = HistoryField.amongOthers (offer.Remembered |> List.map EnteredText.value) last
    field.X <- 1
    field.Y <- 1
    field.Width <- Dim.Fill 1
    field.Height <- 1
    field.Id <- TextId
    field.SelectAll() |> ignore
    DialogStyle.typedInto field

let field (dialog: Dialog) = SubView.byId<TextField> TextId dialog

let checkBox (dialog: Dialog) box =
    SubView.byId<CheckBox> (boxId box) dialog

let private ticked dialog box =
    (checkBox dialog box).Value = CheckState.Checked

let private wholeWordsChosen dialog =
    (checkBox dialog WordsBox).Enabled && ticked dialog WordsBox

let private chosen (dialog: Dialog) =
    { Typed = (field dialog).Text
      Case = if ticked dialog CaseBox then MatchingCase else IgnoringCase
      Words = if wholeWordsChosen dialog then WholeWords else Anywhere
      TypedAs = if ticked dialog ExpressionBox then AsExpression else AsText
      Direction = if ticked dialog BackwardsBox then Backwards else Forwards }

let private refusal dialog =
    match ViewerSearch.query (chosen dialog) with
    | Some(Error error) -> Some error
    | Some(Ok _)
    | None -> None

let private offerWholeWords dialog =
    (checkBox dialog WordsBox).Enabled <- not (ticked dialog ExpressionBox)

type SearchView(refuse: PatternError -> unit) =
    inherit Dialog()

    override this.OnAccepting(args: CommandEventArgs) =
        match refusal this with
        | Some error ->
            refuse error
            (field this).SetFocus() |> ignore
            args.Handled <- true
            true
        | None -> base.OnAccepting args

let create (refuse: PatternError -> unit) (offer: SearchOffer) =
    let dialog =
        new SearchView(refuse, Title = Title, Width = Width, Height = Height)
        |> DialogStyle.dress

    dialog.Add(new Label(X = 1, Y = 0, Height = 1, Text = "Search for:"), input offer)

    placement
    |> List.iter (fun placed -> dialog.Add(checking offer.Last placed) |> ignore)

    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    offerWholeWords dialog
    (checkBox dialog ExpressionBox).ValueChanged.Add(fun _ -> offerWholeWords dialog)
    dialog

let answer (dialog: Dialog) =
    if dialog.Canceled then None else Some(chosen dialog)
