module Marbas.Ui.Messages.ContentErrorMessage

open Marbas.FileSystem

[<Literal>]
let Title = "Cannot view file"

let describe error =
    match error with
    | FileNotFound path -> $"'{FileSystemPath.value path}' no longer exists."
    | AccessDenied path -> $"Access denied to '{FileSystemPath.value path}'."
    | ReadFailed(path, reason) -> $"Could not read '{FileSystemPath.value path}': {reason}"
