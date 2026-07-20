module Marbas.Ui.ListingErrorMessage

open Marbas.FileSystem.DirectoryListing

[<Literal>]
let Title = "Cannot open directory"

let describe error =
    match error with
    | DirectoryNotFound path -> $"'{path}' no longer exists."
    | AccessDenied path -> $"Access denied to '{path}'."
    | ListingFailed(path, reason) -> $"Could not read '{path}': {reason}"
