namespace Marbas.FileSystem

open System

type LogFileError =
    | LogAccessDenied of path: FileSystemPath
    | LogWriteFailed of path: FileSystemPath * message: string

module LogFileError =

    let mapException path (ex: exn) =
        match ex with
        | :? UnauthorizedAccessException -> LogAccessDenied path
        | _ -> LogWriteFailed(path, ex.Message)
