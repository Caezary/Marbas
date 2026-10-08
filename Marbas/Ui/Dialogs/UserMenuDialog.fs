module Marbas.Ui.Dialogs.UserMenuDialog

open System
open System.Collections.ObjectModel
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui
open Marbas.Ui.Dialogs.MenuEntryForm
open Marbas.Ui.Dialogs.MenuRemoval
open Marbas.Ui.Format
open Marbas.Ui.Keys
open Marbas.Ui.Theme

[<Literal>]
let Title = "User menu"

[<Literal>]
let RunHint = "A letter or Enter: run    Esc: close"

[<Literal>]
let EditHint = "Ins: add    F4: change    Del: remove    Ctrl+↑↓: move"

[<Literal>]
let private SizeShare = 60

[<Literal>]
let private TallestList = 20

[<Literal>]
let private FrameHeight = 6

type MenuEditing =
    { AskEntry: EntryPrompt -> EntryAnswer option
      AskRemoval: RemovalPrompt -> RemovalChoice option
      Change: MenuChange -> UserMenu option }

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

let private commands (menu: UserMenu) =
    UserMenu.items menu
    |> List.choose (fun item ->
        match item with
        | CommandItem command -> Some command
        | SeparatorItem -> None)

let private commandPositions (menu: UserMenu) =
    UserMenu.items menu
    |> List.indexed
    |> List.choose (fun (index, item) ->
        match item with
        | CommandItem _ -> Some index
        | SeparatorItem -> None)

let private positionOf hotkey (menu: UserMenu) =
    UserMenu.items menu
    |> List.tryFindIndex (fun item ->
        match item with
        | CommandItem command -> command.Hotkey = hotkey
        | SeparatorItem -> false)

let private nearestCommand index (menu: UserMenu) =
    let positions = commandPositions menu

    positions
    |> List.tryFind (fun position -> position >= index)
    |> Option.orElse (List.tryLast positions)

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
    commands dialog.Menu
    |> List.tryFind (fun command -> command.Hotkey = hotkey)
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

type private Editing =
    { Asked: MenuEditing
      List: ListView
      Dialog: UserMenuView }

let private resized (editing: Editing) (menu: UserMenu) =
    editing.List.Height <- Dim.Absolute(listHeight menu)
    editing.Dialog.Height <- Dim.Absolute(listHeight menu + FrameHeight)

let private changed (editing: Editing) change (following: MenuHotkey option) =
    let before = cursorOf editing.List |> Option.defaultValue 0

    editing.Asked.Change change
    |> Option.iter (fun menu ->
        editing.Dialog.Menu <- menu
        resized editing menu
        fill editing.List editing.Dialog

        following
        |> Option.bind (fun hotkey -> positionOf hotkey menu)
        |> Option.orElse (nearestCommand before menu)
        |> Option.iter (standOn editing.List)

        editing.Dialog.SetNeedsDraw())

let private underCursor (editing: Editing) =
    cursorOf editing.List
    |> Option.bind (fun index -> commandAt index editing.Dialog.Menu |> Option.map (fun command -> index, command))

let private referenceAt index (editing: Editing) =
    index |> Option.bind (fun at -> UserMenu.refAt at editing.Dialog.Menu)

let private standingHotkey (editing: Editing) =
    underCursor editing |> Option.map (fun (_, command) -> command.Hotkey)

let private lineAddedAt index (editing: Editing) =
    changed editing (AddItem(SeparatorItem, referenceAt index editing)) (standingHotkey editing)

let private added (editing: Editing) answer =
    let cursor = cursorOf editing.List

    match answer with
    | CommandEntered command ->
        changed editing (AddItem(CommandItem command, referenceAt cursor editing)) (Some command.Hotkey)
    | LineEntered BelowCursor -> lineAddedAt cursor editing
    | LineEntered AboveCursor -> lineAddedAt (cursor |> Option.map (fun at -> at - 1)) editing

let private adding (editing: Editing) =
    { Editing = Adding
      Others = commands editing.Dialog.Menu }
    |> editing.Asked.AskEntry
    |> Option.iter (added editing)

let private rewritten (editing: Editing) (command: MenuCommand) answer =
    match answer with
    | CommandEntered rewrite -> changed editing (ChangeCommand(command.Hotkey, rewrite)) (Some rewrite.Hotkey)
    | LineEntered _ -> ()

let private rewriting (editing: Editing) =
    underCursor editing
    |> Option.iter (fun (_, command) ->
        { Editing = Changing command
          Others = commands editing.Dialog.Menu |> List.filter ((<>) command) }
        |> editing.Asked.AskEntry
        |> Option.iter (rewritten editing command))

let private removed (editing: Editing) index (command: MenuCommand) choice =
    let following =
        match choice with
        | RemoveCommand -> None
        | RemoveLine _ -> Some command.Hotkey

    MenuRemoval.changeFor index editing.Dialog.Menu choice
    |> Option.iter (fun change -> changed editing change following)

let private removing (editing: Editing) =
    underCursor editing
    |> Option.iter (fun (index, command) ->
        { Command = command
          Offered = MenuRemoval.offered index editing.Dialog.Menu }
        |> editing.Asked.AskRemoval
        |> Option.iter (removed editing index command))

let private moving (editing: Editing) move =
    underCursor editing
    |> Option.iter (fun (_, command) ->
        changed editing (MoveItem(CommandAt command.Hotkey, move)) (Some command.Hotkey))

let private stepOf (key: Key) =
    if key = Key.CursorDown then Some Downward
    elif key = Key.CursorUp then Some Upward
    else None

let private editingAct (editing: Editing) (key: Key) =
    if key = KeyMap.userMenuAddKey then Some(fun () -> adding editing)
    elif key = KeyMap.userMenuChangeKey then Some(fun () -> rewriting editing)
    elif key = KeyMap.userMenuRemoveKey then Some(fun () -> removing editing)
    elif key = KeyMap.userMenuEarlierKey then Some(fun () -> moving editing Earlier)
    elif key = KeyMap.userMenuLaterKey then Some(fun () -> moving editing Later)
    else None

let private answer (editing: Editing) (key: Key) =
    let handled act =
        key.Handled <- true
        act ()

    match KeyMap.menuHotkeyOf key, stepOf key, editingAct editing key with
    | Some hotkey, _, _ -> handled (fun () -> runningOn editing.Dialog hotkey)
    | None, Some direction, _ -> handled (fun () -> stepping editing.List editing.Dialog direction)
    | None, None, Some act -> handled act
    | None, None, None when key = KeyMap.userMenuRunKey ->
        handled (fun () -> runningUnderCursor editing.List editing.Dialog)
    | None, None, None -> ()

let private hintLine (y: Pos) text =
    new Label(X = 1, Y = y, Height = 1, Text = text) |> LiteralText.showing

let create (asked: MenuEditing) (menu: UserMenu) =
    let dialog = new UserMenuView(menu)
    DialogStyle.dress dialog |> ignore

    let list =
        new ListView(X = 1, Y = 1, Width = Dim.Fill 1, Height = listHeight menu, KeystrokeNavigator = null)

    let editing =
        { Asked = asked
          List = list
          Dialog = dialog }

    fill list dialog
    list.FrameChanged.Add(fun _ -> fill list dialog)
    list.KeyDown.Add(answer editing)

    dialog.Add(
        list,
        hintLine (Pos.Bottom list + Pos.Absolute 1) RunHint,
        hintLine (Pos.Bottom list + Pos.Absolute 2) EditHint
    )

    dialog

let entries (dialog: UserMenuView) = SubView.byType<ListView> dialog

let chosen (dialog: UserMenuView) = dialog.Chosen
