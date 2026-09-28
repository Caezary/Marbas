module Marbas.Ui.Messages.HistoryErrorMessage

open Marbas.FileSystem

[<Literal>]
let Title = "History not saved"

let describe error =
    match error with
    | HistoryAccessDenied path -> $"Access denied to '{FileSystemPath.value path}'."
    | HistoryFailed(path, reason) -> $"Could not read or write '{FileSystemPath.value path}': {reason}"
