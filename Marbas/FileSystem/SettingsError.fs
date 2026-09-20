namespace Marbas.FileSystem

open System

type SettingsError =
    | SettingsAccessDenied of path: FileSystemPath
    | SettingsWriteFailed of path: FileSystemPath * message: string

module SettingsError =

    let mapException path (ex: exn) =
        match ex with
        | :? UnauthorizedAccessException -> SettingsAccessDenied path
        | _ -> SettingsWriteFailed(path, ex.Message)
