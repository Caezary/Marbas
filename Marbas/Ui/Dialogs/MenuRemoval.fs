module Marbas.Ui.Dialogs.MenuRemoval

open Marbas.Domain
open Marbas.Profile
open Marbas.Ui.Dialogs.MenuEntryForm

[<Literal>]
let Title = "Remove from the menu"

[<Literal>]
let CancelLabel = "_Cancel"

type RemovalChoice =
    | RemoveCommand
    | RemoveLine of LineSide

type RemovalPrompt =
    { Command: MenuCommand
      Offered: RemovalChoice list }

let private neighbour side index =
    match side with
    | AboveCursor -> index - 1
    | BelowCursor -> index + 1

let private lineOn side index (menu: UserMenu) =
    List.tryItem (neighbour side index) (UserMenu.items menu) = Some SeparatorItem

let offered index (menu: UserMenu) =
    match List.tryItem index (UserMenu.items menu) with
    | Some(CommandItem _) ->
        RemoveCommand
        :: ([ AboveCursor; BelowCursor ]
            |> List.filter (fun side -> lineOn side index menu)
            |> List.map RemoveLine)
    | Some SeparatorItem
    | None -> []

let changeFor index (menu: UserMenu) choice =
    let removed =
        match choice with
        | RemoveCommand -> index
        | RemoveLine side -> neighbour side index

    UserMenu.refAt removed menu |> Option.map RemoveItem

let label choice =
    match choice with
    | RemoveCommand -> "_Remove entry"
    | RemoveLine AboveCursor -> "Line _above"
    | RemoveLine BelowCursor -> "Line _below"

let question (prompt: RemovalPrompt) =
    let named = MenuLabel.value prompt.Command.Label

    match prompt.Offered with
    | [ RemoveCommand ] -> $"Remove '{named}' from the menu?"
    | _ -> $"Remove '{named}' from the menu, or a line next to it?"
