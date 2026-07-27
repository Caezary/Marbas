module Marbas.Ui.OperationErrorMessage

open Marbas.FileSystem.Operations

[<Literal>]
let Title = "Operation failed"

let describe error =
    match error with
    | SourceNotFound path -> $"'{path}' no longer exists."
    | TargetExists path -> $"'{path}' already exists."
    | AccessDenied path -> $"Access denied to '{path}'."
    | OperationFailed(path, reason) -> $"Could not complete '{path}': {reason}"
