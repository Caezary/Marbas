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
    | ArchiveFailed error -> ArchiveErrorMessage.describe error
    | EntryOutsideTarget path ->
        $"An entry of the archive would land outside the target directory: '{FileSystemPath.value path}'."
    | ArchiveEntryNotEditable path ->
        $"'{FileSystemPath.value path}' cannot be edited inside the archive. Unpack it first."
    | ArchiveEntryNotRunnable path ->
        $"'{FileSystemPath.value path}' cannot be run inside the archive. Unpack it first."
    | BatchFailed reason -> $"The operation could not be completed: {reason}"
    | OperationFailed(path, reason) -> $"Could not complete '{FileSystemPath.value path}': {reason}"

let describeFailures (results: ItemResult list) =
    results
    |> List.choose (fun item ->
        match item.Outcome with
        | Error error -> Some $"{EntryName.value item.Name}: {describe error}"
        | Ok() -> None)
    |> String.concat "\n"
