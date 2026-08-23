module Marbas.Ui.PropertiesDialog

open System
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem

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

let private stampRow y id caption (stamp: DateTime) =
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
        new CheckBox(X = 1, Y = Pos.Absolute(2 + index), Height = 1, Text = EntryProperties.label flag, Id = flagId flag)

    box.Value <-
        if EntryProperties.has flag properties then
            CheckState.Checked
        else
            CheckState.UnChecked

    box :> View

let create (properties: EntryProperties) =
    let dialog =
        new Dialog(Title = Title, Width = 62, Height = 14) |> DialogStyle.dress

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

    let boxes = EntryProperties.flags |> List.mapi (fun index flag -> flagBox index flag properties)

    let stamps =
        stampRow 7 CreatedId "Created:" properties.Created
        @ stampRow 8 ModifiedId "Modified:" properties.Modified
        @ stampRow 9 AccessedId "Accessed:" properties.Accessed

    dialog.Add(Array.ofList ((name :> View) :: boxes @ stamps)) |> ignore

    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let name (dialog: Dialog) =
    dialog.SubViews
    |> Seq.pick (function
        | :? Label as label when label.Id = NameId -> Some label
        | _ -> None)

let checkBox (dialog: Dialog) flag =
    dialog.SubViews
    |> Seq.pick (function
        | :? CheckBox as box when box.Id = flagId flag -> Some box
        | _ -> None)

let field (dialog: Dialog) id =
    dialog.SubViews
    |> Seq.pick (function
        | :? TextField as input when input.Id = id -> Some input
        | _ -> None)

let private stampOf (dialog: Dialog) id fallback =
    TimestampText.tryParse (field dialog id).Text |> Option.defaultValue fallback

let properties (original: EntryProperties) (dialog: Dialog) =
    if dialog.Canceled then
        None
    else
        let marked =
            EntryProperties.flags
            |> List.fold
                (fun properties flag ->
                    EntryProperties.setFlag flag ((checkBox dialog flag).Value = CheckState.Checked) properties)
                original

        Some
            { marked with
                Created = stampOf dialog CreatedId original.Created
                Modified = stampOf dialog ModifiedId original.Modified
                Accessed = stampOf dialog AccessedId original.Accessed }
