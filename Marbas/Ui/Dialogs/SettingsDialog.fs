module Marbas.Ui.Dialogs.SettingsDialog

open System
open System.Collections.ObjectModel
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.Ui
open Marbas.Ui.Theme

[<Literal>]
let Title = "Settings"

[<Literal>]
let DefaultEditorId = "defaultEditor"

[<Literal>]
let ExtensionId = "extension"

[<Literal>]
let ProgramId = "program"

[<Literal>]
let AddId = "add"

[<Literal>]
let RemoveId = "remove"

[<Literal>]
let CandidateId = "candidate"

[<Literal>]
let RoleId = "role"

[<Literal>]
let CandidateLabel = "_Under cursor"

let roleLabel role =
    match role with
    | ForEditing -> "_Edit"
    | ForOpening -> "_Open"

let sectionLabel role =
    match role with
    | ForEditing -> "Edited with:"
    | ForOpening -> "Opened with:"

let listId role =
    match role with
    | ForEditing -> "editing"
    | ForOpening -> "opening"

let private SectionHeight = 4

type SettingsView(programs: ProgramSettings, candidate: ProgramPath option) as this =
    inherit Dialog()

    let mutable associated = programs
    let mutable refreshing = false

    let defaultEditor =
        new TextField(
            X = 1,
            Y = 1,
            Width = Dim.Fill 1,
            Height = 1,
            Id = DefaultEditorId,
            Text = (programs.DefaultEditor |> Option.map ProgramPath.value |> Option.defaultValue "")
        )
        |> DialogStyle.typedInto

    let extension =
        new TextField(X = 12, Y = 3, Width = 10, Height = 1, Id = ExtensionId)
        |> DialogStyle.typedInto

    let program =
        new TextField(X = 32, Y = 3, Width = Dim.Fill 1, Height = 1, Id = ProgramId)
        |> DialogStyle.typedInto

    let role =
        new OptionSelector(
            X = 12,
            Y = 4,
            Height = 1,
            Id = RoleId,
            Orientation = Orientation.Horizontal,
            Labels = [| roleLabel ForEditing; roleLabel ForOpening |],
            Value = Nullable 0
        )

    let editing =
        new ListView(X = 1, Y = 8, Width = Dim.Fill 1, Height = SectionHeight, Id = listId ForEditing)

    let opening =
        new ListView(X = 1, Y = 13, Width = Dim.Fill 1, Height = SectionHeight, Id = listId ForOpening)

    let add = DialogStyle.button "_Add"

    let remove = DialogStyle.button "_Remove"

    let fromCursor = DialogStyle.button CandidateLabel

    let mutable target = defaultEditor

    let listing section =
        match section with
        | ForEditing -> editing
        | ForOpening -> opening

    let position section =
        match section with
        | ForEditing -> 0
        | ForOpening -> 1

    let chosen () =
        if role.Value.GetValueOrDefault 0 = position ForEditing then
            ForEditing
        else
            ForOpening

    let showing section =
        ProgramSettings.inRole section associated
        |> List.map AssociationRow.describe
        |> ObservableCollection
        |> (listing section).SetSource

    let show () =
        refreshing <- true
        showing ForEditing
        showing ForOpening
        refreshing <- false

    let fill section index =
        match ProgramSettings.inRole section associated |> List.tryItem index with
        | Some association ->
            extension.Text <- FileExtension.value association.Extension
            program.Text <- ProgramPath.value association.Program
            role.Value <- Nullable(position section)
        | None -> ()

    do
        this.Title <- Title
        this.Width <- Dim.Absolute 70
        this.Height <- Dim.Absolute 21
        DialogStyle.dress this |> ignore

        editing.KeystrokeNavigator <- null
        opening.KeystrokeNavigator <- null
        add.X <- 12
        add.Y <- 5
        add.Id <- AddId
        remove.X <- Pos.Right add + Pos.Absolute 2
        remove.Y <- 5
        remove.Id <- RemoveId
        fromCursor.X <- Pos.Right remove + Pos.Absolute 2
        fromCursor.Y <- 5
        fromCursor.Id <- CandidateId
        fromCursor.Enabled <- candidate.IsSome

        this.Add(
            new Label(X = 1, Y = 0, Height = 1, Text = "Default editor:"),
            defaultEditor,
            new Label(X = 1, Y = 3, Height = 1, Text = "Extension:"),
            extension,
            new Label(X = 24, Y = 3, Height = 1, Text = "Program:"),
            program,
            new Label(X = 1, Y = 4, Height = 1, Text = "Used for:"),
            role,
            add,
            remove,
            fromCursor,
            new Label(X = 1, Y = 7, Height = 1, Text = sectionLabel ForEditing),
            editing,
            new Label(X = 1, Y = 12, Height = 1, Text = sectionLabel ForOpening),
            opening
        )
        |> ignore

        let associating (args: CommandEventArgs) =
            args.Handled <- true

            match FileExtension.tryCreate extension.Text, ProgramPath.tryCreate program.Text with
            | Some named, Some picked ->
                associated <- ProgramSettings.associate (chosen ()) named picked associated
                show ()
            | _ -> ()

        add.Accepting.Add associating
        extension.Accepting.Add associating
        program.Accepting.Add associating

        remove.Accepting.Add(fun args ->
            args.Handled <- true

            match FileExtension.tryCreate extension.Text with
            | Some named ->
                associated <- ProgramSettings.forget (chosen ()) named associated
                show ()
            | None -> ())

        for field in [ defaultEditor; program ] do
            field.HasFocusChanged.Add(fun _ ->
                if field.HasFocus then
                    target <- field)

        fromCursor.Accepting.Add(fun args ->
            args.Handled <- true
            candidate |> Option.iter (fun picked -> target.Text <- ProgramPath.value picked))

        for section in [ ForEditing; ForOpening ] do
            (listing section)
                .ValueChanged.Add(fun args ->
                    if not refreshing then
                        fill section (args.NewValue.GetValueOrDefault 0))

        show ()

        this.AddButton(DialogStyle.button "_Cancel")
        this.AddButton(DialogStyle.button "_Ok")

    member _.Programs =
        { associated with
            DefaultEditor = ProgramPath.tryCreate defaultEditor.Text }

let create (programs: ProgramSettings) (candidate: ProgramPath option) = new SettingsView(programs, candidate)

let field (dialog: Dialog) id = SubView.byId<TextField> id dialog

let button (dialog: Dialog) id = SubView.byId<Button> id dialog

let selector (dialog: Dialog) =
    SubView.byId<OptionSelector> RoleId dialog

let known (dialog: Dialog) role =
    SubView.byId<ListView> (listId role) dialog

let programs (dialog: SettingsView) =
    if dialog.Canceled then None else Some dialog.Programs
