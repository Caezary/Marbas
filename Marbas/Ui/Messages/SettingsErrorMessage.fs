module Marbas.Ui.Messages.SettingsErrorMessage

open Marbas.FileSystem

[<Literal>]
let Title = "Session not saved"

let describe error =
    match error with
    | SettingsAccessDenied path -> $"Access denied to '{FileSystemPath.value path}'."
    | SettingsWriteFailed(path, reason) -> $"Could not write '{FileSystemPath.value path}': {reason}"
