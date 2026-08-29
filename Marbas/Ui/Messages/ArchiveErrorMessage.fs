module Marbas.Ui.Messages.ArchiveErrorMessage

open Marbas.FileSystem

[<Literal>]
let Title = "Archive"

let describe error =
    match error with
    | KindCannotBeRead path -> $"'{FileSystemPath.value path}' is not an archive Marbas can read."
    | ArchiveNotFound path -> $"'{FileSystemPath.value path}' no longer exists."
    | PasswordNeeded path -> $"'{FileSystemPath.value path}' needs a password."
    | ArchiveDamaged(path, reason) -> $"'{FileSystemPath.value path}' cannot be read: {reason}"
