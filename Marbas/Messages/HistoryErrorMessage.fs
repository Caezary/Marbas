module Marbas.Messages.HistoryErrorMessage

open Marbas.FileSystem
open Marbas.Profile

[<Literal>]
let Title = "History not saved"

let describe error =
    match error with
    | HistoryAccessDenied path -> $"Access denied to '{FileSystemPath.value path}'."
    | HistoryFailed(path, reason) -> $"Could not read or write '{FileSystemPath.value path}': {reason}"
