namespace Marbas.FileSystem

open System
open Marbas.Domain

type MenuRefusal =
    | HotkeyTaken of hotkey: MenuHotkey
    | CommandGone of hotkey: MenuHotkey

type UserMenuError =
    | UserMenuAccessDenied of path: FileSystemPath
    | UserMenuFailed of path: FileSystemPath * message: string
    | MenuRefused of refusal: MenuRefusal

module UserMenuError =

    let mapException path (ex: exn) =
        match ex with
        | :? UnauthorizedAccessException -> UserMenuAccessDenied path
        | _ -> UserMenuFailed(path, ex.Message)
