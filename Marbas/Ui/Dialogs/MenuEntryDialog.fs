module Marbas.Ui.Dialogs.MenuEntryDialog

open System
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Profile
open Marbas.Ui
open Marbas.Ui.Dialogs.MenuEntryForm
open Marbas.Ui.Theme

[<Literal>]
let AddTitle = "Add to the menu"

[<Literal>]
let ChangeTitle = "Change the entry"

[<Literal>]
let LabelId = "menuEntryLabel"

[<Literal>]
let HotkeyId = "menuEntryHotkey"

[<Literal>]
let LineId = "menuEntryLine"

[<Literal>]
let RunId = "menuEntryRun"

[<Literal>]
let KindId = "menuEntryKind"

[<Literal>]
let AdministratorId = "menuEntryAdministrator"

[<Literal>]
let ReasonId = "menuEntryReason"

[<Literal>]
let private SecondColumn = 32

[<Literal>]
let private Width = 66

[<Literal>]
let private Height = 19

[<Literal>]
let private HotkeyWidth = 3

let private runs = [ Directly; InShellClosing; InShellKeptOpen ]

let runLabel run =
    match run with
    | Directly -> "_Directly"
    | InShellClosing -> "Shell, window _closes"
    | InShellKeptOpen -> "Shell, window _stays"

type private Kind =
    | AnEntry
    | ALine of LineSide

let private kinds = [ AnEntry; ALine AboveCursor; ALine BelowCursor ]

let private kindLabel kind =
    match kind with
    | AnEntry -> "An _entry"
    | ALine AboveCursor -> "A line a_bove"
    | ALine BelowCursor -> "A line be_low"

let private chosen options (selector: OptionSelector) =
    selector.Value
    |> Option.ofNullable
    |> Option.bind (fun index -> List.tryItem index options)
    |> Option.defaultValue (List.head options)

let field (dialog: Dialog) id = SubView.byId<TextField> id dialog

let runSelector (dialog: Dialog) =
    SubView.byId<OptionSelector> RunId dialog

let kindSelector (dialog: Dialog) =
    SubView.byId<OptionSelector> KindId dialog

let administratorBox (dialog: Dialog) =
    SubView.byId<CheckBox> AdministratorId dialog

let reason (dialog: Dialog) = SubView.byId<Label> ReasonId dialog

let private fieldsIn (dialog: Dialog) =
    { LabelText = (field dialog LabelId).Text
      HotkeyText = (field dialog HotkeyId).Text
      LineText = (field dialog LineId).Text
      ChosenRun = chosen runs (runSelector dialog)
      ChosenElevation =
        if (administratorBox dialog).Value = CheckState.Checked then
            AsAdministrator
        else
            AsUser }

let private entered (prompt: EntryPrompt) (dialog: Dialog) =
    match chosen kinds (kindSelector dialog) with
    | ALine side -> Ok(LineEntered side)
    | AnEntry -> MenuEntryForm.read prompt.Others (fieldsIn dialog) |> Result.map CommandEntered

type EntryView(prompt: EntryPrompt) =
    inherit Dialog()

    member _.Prompt = prompt

    override this.OnAccepting(args: CommandEventArgs) =
        match entered prompt this with
        | Error refusal ->
            (reason this).Text <- MenuEntryForm.describe refusal
            args.Handled <- true
            true
        | Ok _ -> base.OnAccepting args

let private typedField id x y width text =
    new TextField(X = Pos.Absolute x, Y = Pos.Absolute y, Width = width, Height = 1, Id = id, Text = text)
    |> DialogStyle.typedInto

let private selecting id x labels index =
    let selector =
        new OptionSelector(X = Pos.Absolute x, Y = 9, Id = id, Labels = List.toArray labels)

    selector.Value <- Nullable index
    selector

let private offerFields (dialog: Dialog) =
    let forEntry = chosen kinds (kindSelector dialog) = AnEntry

    [ field dialog LabelId :> View
      field dialog HotkeyId
      field dialog LineId
      runSelector dialog
      administratorBox dialog ]
    |> List.iter (fun view -> view.Enabled <- forEntry)

    (reason dialog).Text <- ""

let private startingFields editing =
    match editing with
    | Changing command -> MenuEntryForm.fieldsOf command
    | Adding ->
        { LabelText = ""
          HotkeyText = ""
          LineText = ""
          ChosenRun = Directly
          ChosenElevation = AsUser }

let private titleOf editing =
    match editing with
    | Adding -> AddTitle
    | Changing _ -> ChangeTitle

let private offersLines editing =
    match editing with
    | Adding -> true
    | Changing _ -> false

let create (prompt: EntryPrompt) =
    let start = startingFields prompt.Editing

    let dialog =
        new EntryView(prompt, Title = titleOf prompt.Editing, Width = Width, Height = Height)

    DialogStyle.dress dialog |> ignore

    let kind = selecting KindId SecondColumn (kinds |> List.map kindLabel) 0
    kind.Visible <- offersLines prompt.Editing

    let administrator =
        new CheckBox(
            X = 1,
            Y = 13,
            Height = 1,
            Id = AdministratorId,
            Text = "As _administrator",
            Value =
                (match start.ChosenElevation with
                 | AsAdministrator -> CheckState.Checked
                 | AsUser -> CheckState.UnChecked)
        )

    dialog.Add(
        new Label(X = 1, Y = 0, Height = 1, Text = "Label:"),
        typedField LabelId 1 1 (Dim.Fill 1) start.LabelText,
        new Label(X = 1, Y = 3, Height = 1, Text = "Hotkey:"),
        typedField HotkeyId 10 3 (Dim.Absolute HotkeyWidth) start.HotkeyText,
        new Label(X = 1, Y = 5, Height = 1, Text = "Command line:"),
        typedField LineId 1 6 (Dim.Fill 1) start.LineText,
        new Label(X = 1, Y = 8, Height = 1, Text = "Runs:"),
        selecting RunId 1 (runs |> List.map runLabel) (List.findIndex ((=) start.ChosenRun) runs),
        new Label(X = Pos.Absolute SecondColumn, Y = 8, Height = 1, Text = "Adds:", Visible = kind.Visible),
        kind,
        administrator,
        new Label(X = 1, Y = 14, Width = Dim.Fill 1, Height = 1, Id = ReasonId, Text = "")
    )

    kind.ValueChanged.Add(fun _ -> offerFields dialog)
    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let answer (dialog: EntryView) =
    if dialog.Canceled then
        None
    else
        entered dialog.Prompt dialog |> Result.toOption
