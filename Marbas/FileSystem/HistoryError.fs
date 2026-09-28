namespace Marbas.FileSystem

open System

type HistoryError =
    | HistoryAccessDenied of path: FileSystemPath
    | HistoryFailed of path: FileSystemPath * message: string

module HistoryError =

    let mapException path (ex: exn) =
        match ex with
        | :? UnauthorizedAccessException -> HistoryAccessDenied path
        | _ -> HistoryFailed(path, ex.Message)
