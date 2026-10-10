module Marbas.Messages.UserMenuErrorMessage

open Marbas.Domain
open Marbas.FileSystem

[<Literal>]
let Title = "User menu"

let private named hotkey = $"'{MenuHotkey.value hotkey}'"

let describe error =
    match error with
    | UserMenuAccessDenied path -> $"Access denied to '{FileSystemPath.value path}'."
    | UserMenuFailed(path, reason) -> $"Could not read or write '{FileSystemPath.value path}': {reason}"
    | MenuRefused(HotkeyTaken hotkey) -> $"Another entry already runs on {named hotkey}."
    | MenuRefused(CommandGone hotkey) -> $"The entry on {named hotkey} was removed in the meantime."
