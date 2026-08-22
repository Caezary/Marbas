namespace Marbas.FileSystem

open System

type ContentError =
    | FileNotFound of path: FileSystemPath
    | AccessDenied of path: FileSystemPath
    | ReadFailed of path: FileSystemPath * message: string

module ContentError =

    let mapException path (ex: exn) =
        match ex with
        | :? UnauthorizedAccessException -> AccessDenied path
        | _ -> ReadFailed(path, ex.Message)
