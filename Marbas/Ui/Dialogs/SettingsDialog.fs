module Marbas.Ui.Dialogs.SettingsDialog

open System.Collections.ObjectModel
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui.Theme

[<Literal>]
let Title = "Settings"

[<Literal>]
let DefaultEditorId = "defaultEditor"

[<Literal>]
let ExtensionId = "extension"

[<Literal>]
let EditorId = "editor"

[<Literal>]
let AddId = "add"

[<Literal>]
let RemoveId = "remove"

[<Literal>]
let CandidateId = "candidate"

[<Literal>]
let CandidateLabel = "_Under cursor"

type SettingsView(editors: EditorSettings, candidate: EditorPath option) as this =
    inherit Dialog()

    let mutable associated = editors

    let defaultEditor =
        new TextField(
            X = 1,
            Y = 1,
            Width = Dim.Fill 1,
            Height = 1,
            Id = DefaultEditorId,
            Text = (editors.DefaultEditor |> Option.map EditorPath.value |> Option.defaultValue "")
        )
        |> DialogStyle.typedInto

    let extension =
        new TextField(X = 12, Y = 3, Width = 10, Height = 1, Id = ExtensionId) |> DialogStyle.typedInto

    let editor =
        new TextField(X = 32, Y = 3, Width = Dim.Fill 1, Height = 1, Id = EditorId) |> DialogStyle.typedInto

    let known = new ListView(X = 1, Y = 6, Width = Dim.Fill 1, Height = Dim.Fill())

    let add = DialogStyle.button "_Add"

    let remove = DialogStyle.button "_Remove"

    let fromCursor = DialogStyle.button CandidateLabel

    let mutable target = defaultEditor

    let show () =
        associated.Associations
        |> List.map AssociationRow.describe
        |> ObservableCollection
        |> known.SetSource

    do
        this.Title <- Title
        this.Width <- Dim.Absolute 70
        this.Height <- Dim.Absolute 17
        DialogStyle.dress this |> ignore

        known.KeystrokeNavigator <- null
        add.X <- 12
        add.Y <- 4
        add.Id <- AddId
        remove.X <- Pos.Right add + Pos.Absolute 2
        remove.Y <- 4
        remove.Id <- RemoveId
        fromCursor.X <- Pos.Right remove + Pos.Absolute 2
        fromCursor.Y <- 4
        fromCursor.Id <- CandidateId
        fromCursor.Enabled <- candidate.IsSome

        this.Add(
            new Label(X = 1, Y = 0, Height = 1, Text = "Default editor:"),
            defaultEditor,
            new Label(X = 1, Y = 3, Height = 1, Text = "Extension:"),
            extension,
            new Label(X = 24, Y = 3, Height = 1, Text = "Editor:"),
            editor,
            add,
            remove,
            fromCursor,
            new Label(X = 1, Y = 5, Height = 1, Text = "Editor by extension:"),
            known
        )
        |> ignore

        let associating (args: CommandEventArgs) =
            args.Handled <- true

            match FileExtension.tryCreate extension.Text, EditorPath.tryCreate editor.Text with
            | Some named, Some chosen ->
                associated <- EditorSettings.associate named chosen associated
                show ()
            | _ -> ()

        add.Accepting.Add associating
        extension.Accepting.Add associating
        editor.Accepting.Add associating

        remove.Accepting.Add(fun args ->
            args.Handled <- true

            match FileExtension.tryCreate extension.Text with
            | Some named ->
                associated <- EditorSettings.forget named associated
                show ()
            | None -> ())

        for field in [ defaultEditor; editor ] do
            field.HasFocusChanged.Add(fun _ -> if field.HasFocus then target <- field)

        fromCursor.Accepting.Add(fun args ->
            args.Handled <- true
            candidate |> Option.iter (fun chosen -> target.Text <- EditorPath.value chosen))

        known.ValueChanged.Add(fun args ->
            match associated.Associations |> List.tryItem (args.NewValue.GetValueOrDefault 0) with
            | Some association ->
                extension.Text <- FileExtension.value association.Extension
                editor.Text <- EditorPath.value association.Editor
            | None -> ())

        show ()

        this.AddButton(DialogStyle.button "_Cancel")
        this.AddButton(DialogStyle.button "_Ok")

    member _.Editors =
        { associated with
            DefaultEditor = EditorPath.tryCreate defaultEditor.Text }

let create (editors: EditorSettings) (candidate: EditorPath option) = new SettingsView(editors, candidate)

let field (dialog: Dialog) id =
    dialog.SubViews
    |> Seq.pick (function
        | :? TextField as input when input.Id = id -> Some input
        | _ -> None)

let button (dialog: Dialog) id =
    dialog.SubViews
    |> Seq.pick (function
        | :? Button as pressed when pressed.Id = id -> Some pressed
        | _ -> None)

let known (dialog: Dialog) =
    dialog.SubViews
    |> Seq.pick (function
        | :? ListView as list -> Some list
        | _ -> None)

let editors (dialog: SettingsView) =
    if dialog.Canceled then None else Some dialog.Editors
