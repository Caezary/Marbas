module Marbas.Ui.Messages.FavoritesErrorMessage

open Marbas.FileSystem

[<Literal>]
let Title = "Favourites not saved"

let describe error =
    match error with
    | FavoritesAccessDenied path -> $"Access denied to '{FileSystemPath.value path}'."
    | FavoritesFailed(path, reason) -> $"Could not read or write '{FileSystemPath.value path}': {reason}"
