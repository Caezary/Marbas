namespace Marbas.Profile

open System
open Marbas.FileSystem

type FavoritesError =
    | FavoritesAccessDenied of path: FileSystemPath
    | FavoritesFailed of path: FileSystemPath * message: string

module FavoritesError =

    let mapException path (ex: exn) =
        match ex with
        | :? UnauthorizedAccessException -> FavoritesAccessDenied path
        | _ -> FavoritesFailed(path, ex.Message)
