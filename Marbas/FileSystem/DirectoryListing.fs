module Marbas.FileSystem.DirectoryListing

open System
open System.IO.Abstractions

type EntryKind =
    | File
    | Directory

type Entry =
    { Name: string
      Kind: EntryKind
      Size: int64 option
      Modified: DateTime }

type ListingError =
    | DirectoryNotFound of path: string
    | AccessDenied of path: string
    | ListingFailed of path: string * message: string

let mapException path (ex: exn) =
    match ex with
    | :? UnauthorizedAccessException -> AccessDenied path
    | _ -> ListingFailed(path, ex.Message)

let private directoryEntry (info: IDirectoryInfo) =
    { Name = info.Name
      Kind = Directory
      Size = None
      Modified = info.LastWriteTime }

let private fileEntry (info: IFileInfo) =
    { Name = info.Name
      Kind = File
      Size = Some info.Length
      Modified = info.LastWriteTime }

let read (fs: IFileSystem) (path: string) : Result<Entry list, ListingError> =
    try
        let dir = fs.DirectoryInfo.New path

        if not dir.Exists then
            Error(DirectoryNotFound path)
        else
            let directories = dir.EnumerateDirectories() |> Seq.map directoryEntry
            let files = dir.EnumerateFiles() |> Seq.map fileEntry
            Ok(Seq.append directories files |> List.ofSeq)
    with ex ->
        Error(mapException path ex)
