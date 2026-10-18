namespace Marbas.Ui.Dialogs

open Marbas.Domain
open Marbas.Profile

type UserMenuState =
    { Menu: UserMenu
      Chosen: MenuCommand option }

module UserMenuState =

    let start (menu: UserMenu) : UserMenuState = { Menu = menu; Chosen = None }

    let holding (menu: UserMenu) (state: UserMenuState) = { state with Menu = menu }

    let choosing (command: MenuCommand) (state: UserMenuState) = { state with Chosen = Some command }

    let rows (width: int) (state: UserMenuState) =
        UserMenu.items state.Menu |> List.map (UserMenuRow.describe width)

    let commands (state: UserMenuState) =
        UserMenu.items state.Menu
        |> List.choose (fun item ->
            match item with
            | CommandItem command -> Some command
            | SeparatorItem -> None)

    let commandAt index (state: UserMenuState) =
        match List.tryItem index (UserMenu.items state.Menu) with
        | Some(CommandItem command) -> Some command
        | _ -> None

    let commandOn hotkey (state: UserMenuState) =
        commands state |> List.tryFind (fun command -> command.Hotkey = hotkey)

    let private commandPositions (state: UserMenuState) =
        UserMenu.items state.Menu
        |> List.indexed
        |> List.choose (fun (index, item) ->
            match item with
            | CommandItem _ -> Some index
            | SeparatorItem -> None)

    let positionOf hotkey (state: UserMenuState) =
        UserMenu.items state.Menu
        |> List.tryFindIndex (fun item ->
            match item with
            | CommandItem command -> command.Hotkey = hotkey
            | SeparatorItem -> false)

    let cursorWithin (cursor: int option) (state: UserMenuState) =
        let positions = commandPositions state
        let standing = Option.defaultValue 0 cursor

        positions
        |> List.tryFind (fun position -> position >= standing)
        |> Option.orElse (List.tryLast positions)

    let stepped direction (cursor: int option) (state: UserMenuState) =
        let positions = commandPositions state
        let standing = Option.defaultValue 0 cursor

        match direction with
        | Downward -> positions |> List.tryFind (fun position -> position > standing)
        | Upward -> positions |> List.tryFindBack (fun position -> position < standing)

    let topRowWithin (height: int) (top: int) (state: UserMenuState) =
        let rows = UserMenu.items state.Menu |> List.length

        top |> min (rows - height) |> max 0
