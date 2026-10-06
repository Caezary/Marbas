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

let private tickedForText (last: WindowSearch.Query) box =
    match box, last.Wanted with
    | CaseBox, _ -> last.Case = MatchingCase
    | WordsBox, WindowSearch.Literal(_, words) -> words = WholeWords
    | WordsBox, WindowSearch.Pattern _ -> false
    | ExpressionBox, WindowSearch.Pattern _ -> true
    | ExpressionBox, WindowSearch.Literal _ -> false
    | BackwardsBox, _ -> last.Direction = Backwards

let private tickedForBytes (last: ByteSearch.Query) box =
    match box with
    | BackwardsBox -> last.Direction = Backwards
    | CaseBox
    | WordsBox
    | ExpressionBox -> false

let private tickedFor last box =
    match last with
    | LastText(Some query) -> tickedForText query box
    | LastBytes(Some query) -> tickedForBytes query box
    | LastText None
    | LastBytes None -> false

let private stateOf ticked =
    if ticked then CheckState.Checked else CheckState.UnChecked

let private checking last (box, x, y) =
    new CheckBox(
        X = Pos.Absolute x,
        Y = Pos.Absolute y,
        Height = 1,
        Id = boxId box,
        Text = boxLabel box,
        Value = stateOf (tickedFor last box)
    )

let private spelledLast last =
    match last with
    | LastText(Some query) -> WindowSearch.spelled query.Wanted
    | LastBytes(Some query) -> WantedBytes.spelled query.Bytes
    | LastText None
    | LastBytes None -> ""

let private modeOf last =
    match last with
    | LastText _ -> TextMode
    | LastBytes _ -> HexMode

let private input (offer: SearchOffer) =
    let last = spelledLast offer.Last
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

let private typedAs mode dialog =
    match mode, ticked dialog ExpressionBox with
    | HexMode, _ -> AsBytes
    | TextMode, true -> AsExpression
    | TextMode, false -> AsText

let private chosen mode (dialog: Dialog) =
    { Typed = (field dialog).Text
      Case = if ticked dialog CaseBox then MatchingCase else IgnoringCase
      Words = if wholeWordsChosen dialog then WholeWords else Anywhere
      TypedAs = typedAs mode dialog
      Direction = if ticked dialog BackwardsBox then Backwards else Forwards }

let private refusal mode dialog =
    match ViewerSearch.query (chosen mode dialog) with
    | Some(Error error) -> Some error
    | Some(Ok _)
    | None -> None

let private textOptions = [ CaseBox; WordsBox; ExpressionBox ]

let private offerOptions mode dialog =
    match mode with
    | HexMode -> textOptions |> List.iter (fun box -> (checkBox dialog box).Enabled <- false)
    | TextMode -> (checkBox dialog WordsBox).Enabled <- not (ticked dialog ExpressionBox)

type SearchView(refuse: SearchRefusal -> unit, mode: ViewerMode) =
    inherit Dialog()

    member _.Mode = mode

    override this.OnAccepting(args: CommandEventArgs) =
        match refusal mode this with
        | Some error ->
            refuse error
            (field this).SetFocus() |> ignore
            args.Handled <- true
            true
        | None -> base.OnAccepting args

let create (refuse: SearchRefusal -> unit) (offer: SearchOffer) =
    let mode = modeOf offer.Last

    let dialog =
        new SearchView(refuse, mode, Title = Title, Width = Width, Height = Height)

    DialogStyle.dress dialog |> ignore
    dialog.Add(new Label(X = 1, Y = 0, Height = 1, Text = "Search for:"), input offer)

    placement
    |> List.iter (fun placed -> dialog.Add(checking offer.Last placed) |> ignore)

    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    offerOptions mode dialog
    (checkBox dialog ExpressionBox).ValueChanged.Add(fun _ -> offerOptions mode dialog)
    dialog

let answer (dialog: SearchView) =
    if dialog.Canceled then None else Some(chosen dialog.Mode dialog)
