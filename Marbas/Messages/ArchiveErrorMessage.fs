module Marbas.Messages.ArchiveErrorMessage

open Marbas.FileSystem

[<Literal>]
let Title = "Archive"

let describe error =
    match error with
    | KindCannotBeRead path -> $"'{FileSystemPath.value path}' is not an archive Marbas can read."
    | KindCannotBeChanged path -> $"'{FileSystemPath.value path}' is not an archive Marbas can change."
    | ProtectedArchiveCannotBeChanged path ->
        $"'{FileSystemPath.value path}' is protected by a password and cannot be changed."
    | ArchiveNotFound path -> $"'{FileSystemPath.value path}' no longer exists."
    | ArchiveAccessDenied path -> $"Access denied to '{FileSystemPath.value path}'."
    | PasswordNeeded path -> $"'{FileSystemPath.value path}' needs a password."
    | ArchiveDamaged(path, reason) -> $"'{FileSystemPath.value path}' cannot be read: {reason}"

let describeUnverified (refused: ArchiveError list) =
    (refused |> List.map describe) @ [ "The hits inside are listed unverified." ]
    |> String.concat "\n"
