module Marbas.Ui.OperationErrorMessage

open Marbas.FileSystem
open Marbas.FileSystem.Operations

[<Literal>]
let Title = "Operation failed"

let describe error =
    match error with
    | SourceNotFound path -> $"'{FileSystemPath.value path}' no longer exists."
    | TargetExists path -> $"'{FileSystemPath.value path}' already exists."
    | AccessDenied path -> $"Access denied to '{FileSystemPath.value path}'."
    | OperationFailed(path, reason) -> $"Could not complete '{FileSystemPath.value path}': {reason}"
