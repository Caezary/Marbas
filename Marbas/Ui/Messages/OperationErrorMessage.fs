module Marbas.Ui.Messages.OperationErrorMessage

open Marbas.FileSystem
open Marbas.FileSystem.Operations

[<Literal>]
let Title = "Operation failed"

let describe error =
    match error with
    | SourceNotFound path -> $"'{FileSystemPath.value path}' no longer exists."
    | TargetExists path -> $"'{FileSystemPath.value path}' already exists."
    | AccessDenied path -> $"Access denied to '{FileSystemPath.value path}'."
    | Cancelled path -> $"Stopped before '{FileSystemPath.value path}' was finished."
    | TargetWithinSource path -> $"An archive cannot be created inside what it packs: '{FileSystemPath.value path}'."
    | OperationFailed(path, reason) -> $"Could not complete '{FileSystemPath.value path}': {reason}"

let describeFailures (results: ItemResult list) =
    results
    |> List.choose (fun item ->
        match item.Outcome with
        | Error error -> Some $"{EntryName.value item.Name}: {describe error}"
        | Ok() -> None)
    |> String.concat "\n"
