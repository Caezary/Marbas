module Marbas.FileSystem.DirectoryListing

open System
open System.IO.Abstractions
open Marbas.Domain

type EntryKind =
    | File
    | Directory

type Entry =
    { Name: EntryName
      Kind: EntryKind
      Size: int64 option
      Measured: DirectorySize option
      Modified: DateTimeOffset
      Visibility: EntryVisibility
      Executability: Executability
      LinkTarget: FileSystemPath option }

let ParentName = EntryName.create ".."

type ListingError =
    | DirectoryNotFound of path: FileSystemPath
    | AccessDenied of path: FileSystemPath
    | ListingFailed of path: FileSystemPath * message: string
    | ArchiveUnreadable of error: ArchiveError

let mapException path (ex: exn) =
    match ex with
    | :? UnauthorizedAccessException -> AccessDenied path
    | _ -> ListingFailed(path, ex.Message)

let directoryEntry (info: IDirectoryInfo) =
    let name = EntryName.create info.Name

    { Name = name
      Kind = Directory
      Size = None
      Measured = None
      Modified = DateTimeOffset info.LastWriteTime
      Visibility = EntryVisibility.classify name info.Attributes
      Executability = NotExecutable
      LinkTarget = FileSystemPath.linkTargetOf info }

let fileEntry (info: IFileInfo) =
    let name = EntryName.create info.Name

    { Name = name
      Kind = File
      Size = Some info.Length
      Measured = None
      Modified = DateTimeOffset info.LastWriteTime
      Visibility = EntryVisibility.classify name info.Attributes
      Executability = Executability.classify name info.UnixFileMode
      LinkTarget = FileSystemPath.linkTargetOf info }

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

let private textOrder = StringComparer.InvariantCultureIgnoreCase

let private compareText (left: string) (right: string) = textOrder.Compare(left, right)

let private sizeToSortBy entry =
    match entry.Kind with
    | Directory ->
        entry.Measured
        |> Option.map DirectorySize.bytes
        |> Option.defaultValue 0L
        |> Some
    | File -> entry.Size

let private direction sort =
    match sort.Direction with
    | Ascending -> 1
    | Descending -> -1

let private compareRanked sort compareKeys (groupA, keyA, nameA) (groupB, keyB, nameB) =
    match compare groupA groupB with
    | 0 ->
        match compareKeys keyA keyB with
        | 0 -> direction sort * compareText nameA nameB
        | primary -> direction sort * primary
    | grouped -> grouped

let private arrange sort keyOf compareKeys (entries: Entry seq) =
    entries
    |> Seq.map (fun entry -> (groupRank entry, keyOf entry, EntryName.value entry.Name), entry)
    |> Seq.sortWith (fun (ranked, _) (against, _) -> compareRanked sort compareKeys ranked against)
    |> Seq.map snd
    |> Array.ofSeq

let order (sort: SortOrder) (entries: Entry seq) =
    match sort.Key with
    | ByName -> arrange sort ignore (fun _ _ -> 0) entries
    | ByExtension -> arrange sort (_.Name >> EntryName.extension) compareText entries
    | ByDate -> arrange sort _.Modified compare entries
    | BySize -> arrange sort sizeToSortBy compare entries

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
              Measured = None
              Modified = DateTimeOffset parent.LastWriteTime
              Visibility = Plain
              Executability = NotExecutable
              LinkTarget = None }

let listForDisplay (fs: IFileSystem) (sort: SortOrder) (hidden: HiddenEntries) (path: FileSystemPath) =
    read fs path
    |> Result.map (fun entries ->
        let listed = shown hidden entries

        match parentEntry fs path with
        | Some parent -> order sort (parent :: listed)
        | None -> order sort listed)
