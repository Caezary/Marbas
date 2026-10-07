module Marbas.Ui.Dialogs.UserMenuDialog

open System
open System.Collections.ObjectModel
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Ui.Format
open Marbas.Ui.Keys
open Marbas.Ui.Theme

[<Literal>]
let Title = "User menu"

[<Literal>]
let Hint = "A letter or Enter: run    Esc: close"

[<Literal>]
let private SizeShare = 60

[<Literal>]
let private TallestList = 20

[<Literal>]
let private FrameHeight = 5

let private listHeight (menu: UserMenu) =
    UserMenu.items menu |> List.length |> max 1 |> min TallestList

type UserMenuView(menu: UserMenu) =
    inherit
        Dialog(
            Title = Title,
            Width = Dim.Percent SizeShare,
            Height = Dim.Absolute(listHeight menu + FrameHeight)
        )

    member val Menu = menu with get, set

    member val Chosen: MenuCommand option = None with get, set

let private commandAt index (menu: UserMenu) =
    match List.tryItem index (UserMenu.items menu) with
    | Some(CommandItem command) -> Some command
    | _ -> None

let private commandPositions (menu: UserMenu) =
    UserMenu.items menu
    |> List.indexed
    |> List.choose (fun (index, item) ->
        match item with
        | CommandItem _ -> Some index
        | SeparatorItem -> None)

let private cursorOf (list: ListView) = list.SelectedItem |> Option.ofNullable

let private standOn (list: ListView) index = list.SelectedItem <- Nullable index

let private fill (list: ListView) (dialog: UserMenuView) =
    let cursor = cursorOf list

    UserMenu.items dialog.Menu
    |> List.map (UserMenuRow.describe list.Viewport.Width)
    |> ObservableCollection
    |> list.SetSource

    cursor
    |> Option.orElse (commandPositions dialog.Menu |> List.tryHead)
    |> Option.iter (standOn list)

let private choosing (dialog: UserMenuView) command =
    dialog.Chosen <- Some command
    dialog.RequestStop()

let private runningOn (dialog: UserMenuView) hotkey =
    UserMenu.items dialog.Menu
    |> List.tryPick (fun item ->
        match item with
        | CommandItem command when command.Hotkey = hotkey -> Some command
        | _ -> None)
    |> Option.iter (choosing dialog)

let private runningUnderCursor (list: ListView) (dialog: UserMenuView) =
    cursorOf list
    |> Option.bind (fun index -> commandAt index dialog.Menu)
    |> Option.iter (choosing dialog)

let private stepping (list: ListView) (dialog: UserMenuView) direction =
    let positions = commandPositions dialog.Menu
    let current = cursorOf list |> Option.defaultValue 0

    let next =
        match direction with
        | Downward -> positions |> List.tryFind (fun index -> index > current)
        | Upward -> positions |> List.rev |> List.tryFind (fun index -> index < current)

    next |> Option.iter (standOn list)

let private stepOf (key: Key) =
    if key = Key.CursorDown then Some Downward
    elif key = Key.CursorUp then Some Upward
    else None

let private answer (list: ListView) (dialog: UserMenuView) (key: Key) =
    let handled act =
        key.Handled <- true
        act ()

    match KeyMap.menuHotkeyOf key, stepOf key with
    | Some hotkey, _ -> handled (fun () -> runningOn dialog hotkey)
    | None, Some direction -> handled (fun () -> stepping list dialog direction)
    | None, None when key = KeyMap.userMenuRunKey -> handled (fun () -> runningUnderCursor list dialog)
    | None, None -> ()

let create (menu: UserMenu) =
    let dialog = new UserMenuView(menu)
    DialogStyle.dress dialog |> ignore

    let list =
        new ListView(X = 1, Y = 1, Width = Dim.Fill 1, Height = listHeight menu, KeystrokeNavigator = null)

    let hint =
        new Label(X = 1, Y = Pos.Bottom list + Pos.Absolute 1, Height = 1, Text = Hint)
        |> LiteralText.showing

    fill list dialog
    list.FrameChanged.Add(fun _ -> fill list dialog)
    list.KeyDown.Add(answer list dialog)
    dialog.Add(list, hint)
    dialog

let entries (dialog: UserMenuView) = SubView.byType<ListView> dialog

let chosen (dialog: UserMenuView) = dialog.Chosen
