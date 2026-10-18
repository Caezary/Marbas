module Marbas.Messages.FavoritesErrorMessage

open Marbas.FileSystem
open Marbas.Profile

[<Literal>]
let Title = "Favourites not saved"

let describe error =
    match error with
    | FavoritesAccessDenied path -> $"Access denied to '{FileSystemPath.value path}'."
    | FavoritesFailed(path, reason) -> $"Could not read or write '{FileSystemPath.value path}': {reason}"
