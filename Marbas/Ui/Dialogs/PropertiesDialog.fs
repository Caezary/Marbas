module Marbas.Ui.Dialogs.PropertiesDialog

open System
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Ui.Theme
open Marbas.Ui.Format

[<Literal>]
let Title = "Properties"

[<Literal>]
let NameId = "entryName"

[<Literal>]
let CreatedId = "created"

[<Literal>]
let ModifiedId = "modified"

[<Literal>]
let AccessedId = "accessed"

[<Literal>]
let private CaptionWidth = 11

let flagId flag =
    match flag with
    | ReadOnlyFlag -> "readOnly"
    | HiddenFlag -> "hidden"
    | SystemFlag -> "system"
    | ArchiveFlag -> "archive"

let private stampRow y id caption (stamp: DateTimeOffset) =
    [ new Label(X = 1, Y = Pos.Absolute y, Height = 1, Text = caption) :> View
      new TextField(
          X = Pos.Absolute(1 + CaptionWidth),
          Y = Pos.Absolute y,
          Width = Dim.Fill 1,
          Height = 1,
          Text = TimestampText.format stamp,
          Id = id
      )
      |> DialogStyle.typedInto
      :> View ]

let private flagBox index flag (properties: EntryProperties) =
    let box =
        new CheckBox(
            X = 1,
            Y = Pos.Absolute(2 + index),
            Height = 1,
            Text = EntryProperties.label flag,
            Id = flagId flag
        )

    box.Value <-
        if EntryProperties.has flag properties then
            CheckState.Checked
        else
            CheckState.UnChecked

    box :> View

let create (properties: EntryProperties) =
    let dialog = new Dialog(Title = Title, Width = 62, Height = 14) |> DialogStyle.dress

    let name =
        new Label(
            X = 1,
            Y = 0,
            Width = Dim.Fill 1,
            Height = 1,
            Id = NameId,
            Text = FileSystemPath.value properties.Path
        )
        |> LiteralText.showing

    let boxes =
        EntryProperties.flags
        |> List.mapi (fun index flag -> flagBox index flag properties)

    let stamps =
        stampRow 7 CreatedId "Created:" properties.Created
        @ stampRow 8 ModifiedId "Modified:" properties.Modified
        @ stampRow 9 AccessedId "Accessed:" properties.Accessed

    dialog.Add(Array.ofList ((name :> View) :: boxes @ stamps)) |> ignore

    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let name (dialog: Dialog) = SubView.byId<Label> NameId dialog

let checkBox (dialog: Dialog) flag =
    SubView.byId<CheckBox> (flagId flag) dialog

let field (dialog: Dialog) id = SubView.byId<TextField> id dialog

let private stampOf (dialog: Dialog) id fallback =
    TimestampText.tryParse (field dialog id).Text |> Option.defaultValue fallback

let private marking (dialog: Dialog) properties flag =
    match (checkBox dialog flag).Value with
    | CheckState.Checked -> EntryProperties.withFlag flag properties
    | _ -> EntryProperties.withoutFlag flag properties

let properties (original: EntryProperties) (dialog: Dialog) =
    if dialog.Canceled then
        None
    else
        let marked = EntryProperties.flags |> List.fold (marking dialog) original

        Some
            { marked with
                Created = stampOf dialog CreatedId original.Created
                Modified = stampOf dialog ModifiedId original.Modified
                Accessed = stampOf dialog AccessedId original.Accessed }
