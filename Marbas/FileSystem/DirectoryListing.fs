module Marbas.FileSystem.DirectoryListing

open System
open System.IO.Abstractions

type EntryKind =
    | File
    | Directory

type Entry =
    { Name: EntryName
      Kind: EntryKind
      Size: int64 option
      Modified: DateTime }

let ParentName = EntryName.create ".."

type ListingError =
    | DirectoryNotFound of path: FileSystemPath
    | AccessDenied of path: FileSystemPath
    | ListingFailed of path: FileSystemPath * message: string

let mapException path (ex: exn) =
    match ex with
    | :? UnauthorizedAccessException -> AccessDenied path
    | _ -> ListingFailed(path, ex.Message)

let private directoryEntry (info: IDirectoryInfo) =
    { Name = EntryName.create info.Name
      Kind = Directory
      Size = None
      Modified = info.LastWriteTime }

let private fileEntry (info: IFileInfo) =
    { Name = EntryName.create info.Name
      Kind = File
      Size = Some info.Length
      Modified = info.LastWriteTime }

let read (fs: IFileSystem) (path: FileSystemPath) : Result<Entry list, ListingError> =
    try
        let dir = fs.DirectoryInfo.New(FileSystemPath.value path)

        if not dir.Exists then
            Error(DirectoryNotFound path)
        else
            let directories = dir.EnumerateDirectories() |> Seq.map directoryEntry
            let files = dir.EnumerateFiles() |> Seq.map fileEntry
            Ok(Seq.append directories files |> List.ofSeq)
    with ex ->
        Error(mapException path ex)

let order (entries: Entry list) =
    entries
    |> List.sortWith (fun a b ->
        match a.Kind, b.Kind with
        | Directory, File -> -1
        | File, Directory -> 1
        | _ -> String.Compare(EntryName.value a.Name, EntryName.value b.Name, StringComparison.OrdinalIgnoreCase))

let private parentEntry (fs: IFileSystem) (path: FileSystemPath) =
    match fs.DirectoryInfo.New(FileSystemPath.value path).Parent with
    | null -> None
    | parent ->
        Some
            { Name = ParentName
              Kind = Directory
              Size = None
              Modified = parent.LastWriteTime }

let listForDisplay (fs: IFileSystem) (path: FileSystemPath) =
    read fs path
    |> Result.map (fun entries ->
        match parentEntry fs path with
        | Some parent -> parent :: order entries
        | None -> order entries)
