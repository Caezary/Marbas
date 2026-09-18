module Marbas.Ui.Dialogs.FindDialog

open System
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.Ui.Theme

[<Literal>]
let Title = "Find"

[<Literal>]
let MasksId = "findMasks"

[<Literal>]
let TextId = "findText"

[<Literal>]
let ScopeId = "findScope"

[<Literal>]
let LastResultsTitle = "_Last results"

type FindChoice =
    | Search of FindQuery
    | LastResults

type FindView(count: int option) =
    inherit Dialog()

    member val Reopening = false with get, set

    member _.Count = count

let lastResultsTitle count =
    match count with
    | Some found -> $"%s{LastResultsTitle} (%d{found})"
    | None -> LastResultsTitle

[<Literal>]
let EveryName = "*.*"

[<Literal>]
let private SecondColumn = 26

[<Literal>]
let private Width = 56

[<Literal>]
let private Height = 17

type FindBox =
    | CaseBox
    | WordsBox
    | SenseBox
    | BinaryBox
    | DirectoriesBox
    | HiddenBox
    | LinksBox
    | ArchivesBox

let scopes =
    [ FromDirectory
      DirectoryOnly
      MarkedDirectories
      FromVolumeRoot
      AllDrives
      PathDirectories ]

let scopeLabel scope =
    match scope with
    | FromDirectory -> "_From directory"
    | DirectoryOnly -> "_This directory only"
    | MarkedDirectories -> "_Marked directories"
    | FromVolumeRoot -> "Whole _volume"
    | AllDrives -> "All _drives"
    | PathDirectories -> "_PATH directories"

let boxLabel box =
    match box with
    | CaseBox -> "C_ase sensitive"
    | WordsBox -> "_Whole words"
    | SenseBox -> "_Not containing"
    | BinaryBox -> "_Binary files too"
    | DirectoriesBox -> "D_irectories too"
    | HiddenBox -> "Hidd_en entries"
    | LinksBox -> "Follow lin_ks"
    | ArchivesBox -> "Search a_rchives"

let private textBoxes = [ CaseBox; WordsBox; SenseBox; BinaryBox ]

let private placement =
    [ CaseBox, 1, 4
      WordsBox, SecondColumn, 4
      SenseBox, 1, 5
      BinaryBox, SecondColumn, 5
      DirectoriesBox, SecondColumn, 7
      HiddenBox, SecondColumn, 8
      LinksBox, SecondColumn, 9
      ArchivesBox, SecondColumn, 10 ]

let private boxId box = $"find%O{box}"

let private label x y text =
    new Label(X = Pos.Absolute x, Y = Pos.Absolute y, Height = 1, Text = text)

let private input id y =
    new TextField(X = 1, Y = Pos.Absolute y, Width = Dim.Fill 1, Height = 1, Id = id)
    |> DialogStyle.typedInto

let private checking (box, x, y) =
    new CheckBox(X = Pos.Absolute x, Y = Pos.Absolute y, Height = 1, Id = boxId box, Text = boxLabel box)

let field (dialog: Dialog) id =
    dialog.SubViews
    |> Seq.pick (function
        | :? TextField as input when input.Id = id -> Some input
        | _ -> None)

let selector (dialog: Dialog) =
    dialog.SubViews
    |> Seq.pick (function
        | :? OptionSelector as options when options.Id = ScopeId -> Some options
        | _ -> None)

let checkBox (dialog: Dialog) box =
    dialog.SubViews
    |> Seq.pick (function
        | :? CheckBox as found when found.Id = boxId box -> Some found
        | _ -> None)

let lastResults (dialog: Dialog) = dialog.Buttons |> Seq.head

let private offer (dialog: Dialog) =
    let searching = (field dialog TextId).Text <> ""

    textBoxes |> List.iter (fun box -> (checkBox dialog box).Enabled <- searching)
    (checkBox dialog DirectoriesBox).Enabled <- not searching

let create (ground: MarkedGround) (hidden: HiddenEntries) (count: int option) =
    let dialog =
        new FindView(count, Title = Title, Width = Width, Height = Height)

    DialogStyle.dress dialog |> ignore

    let masks = input MasksId 1
    masks.Text <- EveryName
    masks.SelectAll() |> ignore

    let scope =
        new OptionSelector(X = 1, Y = 7, Id = ScopeId, Labels = (scopes |> List.map scopeLabel |> List.toArray))

    scope.Value <- Nullable 0

    dialog.Add(label 1 0 "File name:", masks, label 1 2 "Containing text:", input TextId 3)
    dialog.Add(label 1 6 "Search in:", scope, label SecondColumn 6 "Also:")
    placement |> List.iter (fun placed -> dialog.Add(checking placed) |> ignore)

    (checkBox dialog HiddenBox).Value <-
        match hidden with
        | Shown -> CheckState.Checked
        | Concealed -> CheckState.UnChecked

    Seq.zip scope.SubViews scopes
    |> Seq.iter (fun (option, which) -> option.Enabled <- which <> MarkedDirectories || ground = DirectoriesMarked)

    offer dialog
    (field dialog TextId).TextChanged.Add(fun _ -> offer dialog)

    let last = DialogStyle.button (lastResultsTitle count)
    last.Enabled <- Option.isSome count
    last.Accepting.Add(fun _ -> dialog.Reopening <- true)

    dialog.AddButton last
    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let private ticked dialog box =
    (checkBox dialog box).Value = CheckState.Checked

let private chosenScope (dialog: Dialog) =
    let index = (selector dialog).Value |> Option.ofNullable |> Option.defaultValue 0

    scopes |> List.tryItem index |> Option.defaultValue FromDirectory

let private textSearch dialog =
    FindText.tryCreate (field dialog TextId).Text
    |> Option.map (fun text ->
        { Text = text
          Case = if ticked dialog CaseBox then MatchingCase else IgnoringCase
          Words = if ticked dialog WordsBox then WholeWords else Anywhere
          Sense = if ticked dialog SenseBox then NotContaining else Containing
          Binary = if ticked dialog BinaryBox then Searched else Skipped })

let private query (dialog: FindView) =
    let targets =
        if ticked dialog DirectoriesBox then
            FilesAndDirectories
        else
            FilesOnly

    { What = FindWhat.create (NameMasks.read (field dialog MasksId).Text) (textSearch dialog) targets
      Scope = chosenScope dialog
      Hidden = if ticked dialog HiddenBox then Shown else Concealed
      Links = if ticked dialog LinksBox then FollowLinks else StopAtLinks
      Archives = if ticked dialog ArchivesBox then SearchArchives else SkipArchives }

let choice (dialog: FindView) =
    match dialog.Reopening, dialog.Canceled with
    | true, _ -> Some LastResults
    | false, true -> None
    | false, false -> Some(Search(query dialog))
