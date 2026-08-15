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

let private groupRank entry =
    if entry.Name = ParentName then 0
    else
        match entry.Kind with
        | Directory -> 1
        | File -> 2

let private compareNames a b =
    String.Compare(EntryName.value a.Name, EntryName.value b.Name, StringComparison.OrdinalIgnoreCase)

let private compareExtensions a b =
    String.Compare(EntryName.extension a.Name, EntryName.extension b.Name, StringComparison.OrdinalIgnoreCase)

let private compareByKey key a b =
    let primary =
        match key with
        | ByName -> 0
        | ByExtension -> compareExtensions a b
        | ByDate -> compare a.Modified b.Modified
        | BySize -> compare a.Size b.Size

    if primary = 0 then compareNames a b else primary

let private direction sort =
    match sort.Direction with
    | Ascending -> 1
    | Descending -> -1

let order (sort: SortOrder) (entries: Entry list) =
    entries
    |> List.sortWith (fun a b ->
        match compare (groupRank a) (groupRank b) with
        | 0 -> direction sort * compareByKey sort.Key a b
        | grouped -> grouped)

let private parentEntry (fs: IFileSystem) (path: FileSystemPath) =
    match fs.DirectoryInfo.New(FileSystemPath.value path).Parent with
    | null -> None
    | parent ->
        Some
            { Name = ParentName
              Kind = Directory
              Size = None
              Modified = parent.LastWriteTime }

let listForDisplay (fs: IFileSystem) (sort: SortOrder) (path: FileSystemPath) =
    read fs path
    |> Result.map (fun entries ->
        match parentEntry fs path with
        | Some parent -> order sort (parent :: entries)
        | None -> order sort entries)
