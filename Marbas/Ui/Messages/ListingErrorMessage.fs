module Marbas.Ui.Messages.ListingErrorMessage

open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing

[<Literal>]
let Title = "Cannot open directory"

let title error =
    match error with
    | DirectoryNotFound _
    | AccessDenied _
    | ListingFailed _ -> Title
    | ArchiveUnreadable _ -> ArchiveErrorMessage.Title

let describe error =
    match error with
    | DirectoryNotFound path -> $"'{FileSystemPath.value path}' no longer exists."
    | AccessDenied path -> $"Access denied to '{FileSystemPath.value path}'."
    | ListingFailed(path, reason) -> $"Could not read '{FileSystemPath.value path}': {reason}"
    | ArchiveUnreadable error -> ArchiveErrorMessage.describe error
