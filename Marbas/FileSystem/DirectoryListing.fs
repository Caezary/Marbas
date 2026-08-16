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
      Modified: DateTime
      Visibility: EntryVisibility }

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
    let name = EntryName.create info.Name

    { Name = name
      Kind = Directory
      Size = None
      Modified = info.LastWriteTime
      Visibility = EntryVisibility.classify name info.Attributes }

let private fileEntry (info: IFileInfo) =
    let name = EntryName.create info.Name

    { Name = name
      Kind = File
      Size = Some info.Length
      Modified = info.LastWriteTime
      Visibility = EntryVisibility.classify name info.Attributes }

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

let shown (hidden: HiddenEntries) (entries: Entry list) =
    entries |> List.filter (_.Visibility >> HiddenEntries.admits hidden)

let private parentEntry (fs: IFileSystem) (path: FileSystemPath) =
    match fs.DirectoryInfo.New(FileSystemPath.value path).Parent with
    | null -> None
    | parent ->
        Some
            { Name = ParentName
              Kind = Directory
              Size = None
              Modified = parent.LastWriteTime
              Visibility = Plain }

let listForDisplay (fs: IFileSystem) (sort: SortOrder) (hidden: HiddenEntries) (path: FileSystemPath) =
    read fs path
    |> Result.map (fun entries ->
        let listed = shown hidden entries

        match parentEntry fs path with
        | Some parent -> order sort (parent :: listed)
        | None -> order sort listed)
