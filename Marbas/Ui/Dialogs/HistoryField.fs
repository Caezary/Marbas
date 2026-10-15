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

let private activeIn (popovers: ApplicationPopover) =
    match popovers.GetActivePopover() with
    | null -> None
    | active -> Some active

let private activeList (app: IApplication) =
    match app.Popovers with
    | null -> None
    | popovers -> activeIn popovers

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
            match app.Popovers with
            | null -> ()
            | popovers ->
                activeIn popovers
                |> Option.filter ofField
                |> Option.iter (fun active ->
                    popovers.Hide active
                    key.Handled <- true))

let private standOnNewest (list: ListView) =
    match list.Source with
    | null -> ()
    | source ->
        if not list.SelectedItem.HasValue && source.Count > 0 then
            list.SelectedItem <- Nullable 0

let private fitsTo (field: View) (popover: Popover<ListView, string>) (list: ListView) =
    list.Width <- Dim.Func(fun _ -> field.Frame.Width)

    popover.VisibleChanged.Add(fun _ ->
        if popover.Visible then
            standOnNewest list)

let private listPopover (registered: obj | null) =
    match registered with
    | :? Popover<ListView, string> as popover -> Some popover
    | _ -> None

let private fitsToItsField (popover: Popover<ListView, string>) =
    match shownFor popover, popover.ContentView with
    | Some field, NonNull list -> fitsTo field popover list
    | _ -> ()

let private fitsListToField (app: IApplication) =
    match app.Popovers with
    | null -> ()
    | popovers ->
        popovers.PopoverRegistered.Add(fun registered -> listPopover registered.Value |> Option.iter fitsToItsField)

let apply (app: IApplication) =
    closesListOnEsc app
    fitsListToField app
