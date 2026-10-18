namespace Marbas.Profile

open System
open Marbas.Domain
open Marbas.FileSystem

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
