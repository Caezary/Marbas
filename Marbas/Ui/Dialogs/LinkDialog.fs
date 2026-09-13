module Marbas.Ui.Dialogs.LinkDialog

open System
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Messages
open Marbas.Ui.Theme

[<Literal>]
let Title = "Link"

[<Literal>]
let TargetId = "linkTarget"

[<Literal>]
let KindId = "linkKind"

[<Literal>]
let ReasonId = "linkReason"

let kinds = [ Symbolic; Hard; Junction ]

let kindLabel kind =
    match kind with
    | Symbolic -> "_Symbolic link"
    | Hard -> "_Hard link"
    | Junction -> "_Junction"

let private chosenKind (selector: OptionSelector) =
    let index = selector.Value |> Option.ofNullable |> Option.defaultValue 0
    kinds |> List.tryItem index |> Option.defaultValue Symbolic

let private offer (refusal: string -> LinkKind -> LinkRefusal option) (target: TextField) (selector: OptionSelector) (reason: Label) =
    let refusals = kinds |> List.map (refusal target.Text)

    Seq.zip selector.SubViews refusals
    |> Seq.iter (fun (option, refused) -> option.Enabled <- Option.isNone refused)

    reason.Text <- refusals |> List.choose (Option.map LinkRefusalMessage.describe) |> String.concat "\n"

    let chosen = chosenKind selector

    if List.zip kinds refusals |> List.exists (fun (kind, refused) -> kind = chosen && refused.IsSome) then
        selector.Value <- Nullable 0

let create (refusal: string -> LinkKind -> LinkRefusal option) (suggestion: string) =
    let dialog =
        new Dialog(Title = Title, Width = 64, Height = 14) |> DialogStyle.dress

    let target =
        new TextField(X = 1, Y = 1, Width = Dim.Fill 1, Height = 1, Text = suggestion, Id = TargetId)
        |> DialogStyle.typedInto

    target.InsertionPoint <- suggestion.Length

    let selector =
        new OptionSelector(X = 1, Y = 4, Id = KindId, Labels = (kinds |> List.map kindLabel |> List.toArray))

    selector.Value <- Nullable 0

    let reason =
        new Label(X = 1, Y = 8, Width = Dim.Fill 1, Height = 2, Id = ReasonId, Text = "")

    dialog.Add(
        new Label(X = 1, Y = 0, Height = 1, Text = "Link at:"),
        target,
        new Label(X = 1, Y = 3, Height = 1, Text = "Kind:"),
        selector,
        reason
    )
    |> ignore

    offer refusal target selector reason
    target.TextChanged.Add(fun _ -> offer refusal target selector reason)

    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let field (dialog: Dialog) =
    dialog.SubViews
    |> Seq.pick (function
        | :? TextField as input when input.Id = TargetId -> Some input
        | _ -> None)

let selector (dialog: Dialog) =
    dialog.SubViews
    |> Seq.pick (function
        | :? OptionSelector as options when options.Id = KindId -> Some options
        | _ -> None)

let reason (dialog: Dialog) =
    dialog.SubViews
    |> Seq.pick (function
        | :? Label as label when label.Id = ReasonId -> Some label
        | _ -> None)

let choice (dialog: Dialog) =
    if dialog.Canceled then
        None
    else
        Some
            { Target = (field dialog).Text
              Kind = dialog |> selector |> chosenKind }
