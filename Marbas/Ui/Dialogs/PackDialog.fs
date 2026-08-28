module Marbas.Ui.Dialogs.PackDialog

open System
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui.Theme

[<Literal>]
let Title = "Pack"

[<Literal>]
let TargetId = "packTarget"

[<Literal>]
let FormatId = "packFormat"

[<Literal>]
let CompressionId = "packCompression"

[<Literal>]
let PasswordId = "packPassword"

[<Literal>]
let private SecondColumn = 26

let formatLabel format =
    match format with
    | Zip -> "_Zip"
    | TarGz -> "_Tar.gz"

let compressionLabel compression =
    match compression with
    | Stored -> "_Store"
    | Normal -> "_Normal"
    | Maximum -> "Ma_ximum"

let private chosen options (selector: OptionSelector) =
    let index = selector.Value |> Option.ofNullable |> Option.defaultValue 0

    options |> List.tryItem index |> Option.defaultValue (List.head options)

let private selecting id x labels chosenIndex =
    let selector =
        new OptionSelector(X = Pos.Absolute x, Y = 4, Id = id, Labels = List.toArray labels)

    selector.Value <- Nullable chosenIndex
    selector

let create (retarget: ArchiveFormat -> string -> string) (suggestion: string) =
    let dialog =
        new Dialog(Title = Title, Width = 64, Height = 14) |> DialogStyle.dress

    let target =
        new TextField(X = 1, Y = 1, Width = Dim.Fill 1, Height = 1, Text = suggestion, Id = TargetId)
        |> DialogStyle.typedInto

    target.InsertionPoint <- suggestion.Length

    let password =
        new TextField(X = 1, Y = 9, Width = Dim.Fill 1, Height = 1, Id = PasswordId, Secret = true)
        |> DialogStyle.typedInto

    let formats =
        selecting
            FormatId
            1
            (ArchiveFormat.all |> List.map formatLabel)
            (ArchiveFormat.all |> List.findIndex ((=) ArchiveFormat.initial))

    let levels =
        selecting
            CompressionId
            SecondColumn
            (ArchiveCompression.all |> List.map compressionLabel)
            (ArchiveCompression.all |> List.findIndex ((=) ArchiveCompression.initial))

    dialog.Add(
        new Label(X = 1, Y = 0, Height = 1, Text = "Pack into:"),
        target,
        new Label(X = 1, Y = 3, Height = 1, Text = "Format:"),
        formats,
        new Label(X = Pos.Absolute SecondColumn, Y = 3, Height = 1, Text = "Compression:"),
        levels,
        new Label(X = 1, Y = 8, Height = 1, Text = "Password:"),
        password
    )
    |> ignore

    formats.ValueChanged.Add(fun _ ->
        let format = chosen ArchiveFormat.all formats
        target.Text <- retarget format target.Text
        password.Enabled <- ArchiveFormat.carriesPassword format)

    dialog.AddButton(DialogStyle.button "_Cancel")
    dialog.AddButton(DialogStyle.button "_Ok")
    dialog

let field (dialog: Dialog) id =
    dialog.SubViews
    |> Seq.pick (function
        | :? TextField as input when input.Id = id -> Some input
        | _ -> None)

let selector (dialog: Dialog) id =
    dialog.SubViews
    |> Seq.pick (function
        | :? OptionSelector as options when options.Id = id -> Some options
        | _ -> None)

let choice (dialog: Dialog) =
    if dialog.Canceled then
        None
    else
        let format = chosen ArchiveFormat.all (selector dialog FormatId)

        Some
            { Target = (field dialog TargetId).Text
              Format = format
              Compression = chosen ArchiveCompression.all (selector dialog CompressionId)
              Password =
                if ArchiveFormat.carriesPassword format then
                    ArchivePassword.tryCreate (field dialog PasswordId).Text
                else
                    None }
