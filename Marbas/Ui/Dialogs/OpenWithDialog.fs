module Marbas.Ui.Dialogs.OpenWithDialog

open System.Collections.ObjectModel
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.Programs
open Marbas.Ui
open Marbas.Ui.Theme

[<Literal>]
let Title = "Open with"

[<Literal>]
let ProgramId = "openWithProgram"

[<Literal>]
let RememberId = "openWithRemember"

[<Literal>]
let private ListHeight = 9

let rememberLabel (name: EntryName) =
    match FileExtension.ofName name with
    | Some extension -> $"_Always open .{FileExtension.value extension} this way"
    | None -> "_Always open this way"

let create (name: EntryName) (handlers: Handler list) (chosen: ProgramPath option) (remembered: string list) =
    let dialog = new Dialog(Title = Title, Width = 76, Height = 19) |> DialogStyle.dress

    let known =
        new ListView(X = 1, Y = 3, Width = Dim.Fill 1, Height = ListHeight, KeystrokeNavigator = null)

    known.SetSource(handlers |> List.map HandlerRow.describe |> ObservableCollection)

    let offered =
        chosen
        |> Option.orElse (handlers |> List.tryHead |> Option.map _.Program)
        |> Option.map ProgramPath.value
        |> Option.defaultValue ""

    let program = HistoryField.amongOthers remembered offered
    program.X <- 1
    program.Y <- 13
    program.Width <- Dim.Fill 1
    program.Height <- 1
    program.Id <- ProgramId
    DialogStyle.typedInto program |> ignore

    let remember =
        new CheckBox(X = 1, Y = 14, Height = 1, Id = RememberId, Text = rememberLabel name)

    remember.Enabled <- (FileExtension.ofName name).IsSome

    dialog.Add(
        new Label(X = 1, Y = 0, Width = Dim.Fill 1, Height = 1, Text = EntryName.value name),
        new Label(X = 1, Y = 2, Height = 1, Text = "Known programs:"),
        known,
        new Label(X = 1, Y = 12, Height = 1, Text = "Program:"),
        program,
        remember
    )
    |> ignore

    chosen
    |> Option.bind (fun path -> handlers |> List.tryFindIndex (fun handler -> handler.Program = path))
    |> Option.iter (fun index -> known.SelectedItem <- index)

    known.ValueChanged.Add(fun args ->
        match handlers |> List.tryItem (args.NewValue.GetValueOrDefault 0) with
        | Some handler -> program.Text <- ProgramPath.value handler.Program
        | None -> ())

    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let field (dialog: Dialog) =
    SubView.byId<TextField> ProgramId dialog

let checkBox (dialog: Dialog) =
    SubView.byId<CheckBox> RememberId dialog

let known (dialog: Dialog) = SubView.byType<ListView> dialog

let private handlerUse (dialog: Dialog) =
    match (checkBox dialog).Value with
    | CheckState.Checked -> AlwaysThisWay
    | _ -> ThisTimeOnly

let choice (dialog: Dialog) =
    if dialog.Canceled then
        None
    else
        ProgramPath.tryCreate (field dialog).Text
        |> Option.map (fun program ->
            { Program = program
              Use = handlerUse dialog })
