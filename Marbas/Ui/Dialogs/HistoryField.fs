module Marbas.Ui.Dialogs.HistoryField

open System
open System.Collections.ObjectModel
open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui.Keys

let private shownFor (active: IPopoverView) =
    match active.Target with
    | null -> None
    | target ->
        match target.TryGetTarget() with
        | true, view -> Some view
        | _ -> None

let private activeList (app: IApplication) =
    match app.Popovers with
    | null -> None
    | popovers ->
        match popovers.GetActivePopover() with
        | null -> None
        | active -> Some active

let private listOf (app: IApplication) (field: View) =
    activeList app
    |> Option.exists (fun active ->
        shownFor active
        |> Option.exists (fun shown -> obj.ReferenceEquals(shown, field)))

let private listShown (field: DropDownList) =
    match field.App with
    | null -> false
    | app -> listOf app field

let create (remembered: string list) (suggestion: string) =
    let field = new DropDownList(ReadOnly = false, Text = suggestion)
    let choosing = ref false
    field.Source <- new ListWrapper<string>(ObservableCollection remembered)
    field.KeyBindings.Remove Key.Space
    field.InsertionPoint <- suggestion.Length

    field.Accepting.Add(fun args ->
        if listShown field then
            choosing.Value <- true
            args.Handled <- true)

    field.Activating.Add(fun _ -> choosing.Value <- listShown field)
    field.KeyDown.Add(fun _ -> choosing.Value <- false)

    field.TextChanged.Add(fun _ ->
        if choosing.Value then
            choosing.Value <- false
            field.InsertionPoint <- field.Text.Length)

    field

let amongOthers (remembered: string list) (suggestion: string) =
    let field = create remembered suggestion
    field.KeyBindings.Remove Key.CursorUp
    field.KeyBindings.Remove Key.CursorDown
    field

let private ofField (active: IPopoverView) =
    shownFor active |> Option.exists (fun shown -> shown :? DropDownList)

let private closesListOnEsc (app: IApplication) =
    app.Keyboard.KeyDown.Add(fun key ->
        if key = KeyMap.cancelKey then
            activeList app
            |> Option.filter ofField
            |> Option.iter (fun active ->
                app.Popovers.Hide active
                key.Handled <- true))

let private standOnNewest (list: ListView) =
    if not list.SelectedItem.HasValue && list.Source <> null && list.Source.Count > 0 then
        list.SelectedItem <- Nullable 0

let private fitsListToField (app: IApplication) =
    app.Popovers.PopoverRegistered.Add(fun registered ->
        match registered.Value with
        | :? Popover<ListView, string> as popover ->
            shownFor popover
            |> Option.iter (fun field ->
                popover.ContentView.Width <- Dim.Func(fun _ -> field.Frame.Width)

                popover.VisibleChanged.Add(fun _ ->
                    if popover.Visible then
                        standOnNewest popover.ContentView))
        | _ -> ())

let apply (app: IApplication) =
    closesListOnEsc app
    fitsListToField app
