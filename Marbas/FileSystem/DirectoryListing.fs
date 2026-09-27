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
      LinkTarget: FileSystemPath option
      Verification: EntryVerification }

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

let private lengthOf (info: IFileSystemInfo) =
    match info with
    | :? IFileInfo as file -> Some file.Length
    | _ -> None

let private kindOf (info: IFileSystemInfo) =
    if info.Attributes.HasFlag IO.FileAttributes.Directory then
        Directory
    else
        File

let entryFromInfo (info: IFileSystemInfo) =
    let name = EntryName.create info.Name

    match kindOf info with
    | Directory ->
        { Name = name
          Kind = Directory
          Size = None
          Measured = None
          Modified = DateTimeOffset info.LastWriteTime
          Visibility = EntryVisibility.classify name (EntryAttributes.visibility info.Attributes)
          Executability = NotExecutable
          LinkTarget = FileSystemPath.linkTargetOfInfo info
          Verification = Verified }
    | File ->
        { Name = name
          Kind = File
          Size = lengthOf info
          Measured = None
          Modified = DateTimeOffset info.LastWriteTime
          Visibility = EntryVisibility.classify name (EntryAttributes.visibility info.Attributes)
          Executability = Executability.classify name (EntryAttributes.executability info.UnixFileMode)
          LinkTarget = FileSystemPath.linkTargetOfInfo info
          Verification = Verified }

type Child =
    { Path: FileSystemPath
      Entry: Entry }

let private childFromInfo (info: IFileSystemInfo) =
    { Path = FileSystemPath.create info.FullName
      Entry = entryFromInfo info }

let children (fs: IFileSystem) (path: FileSystemPath) =
    fs.DirectoryInfo.New(FileSystemPath.value path).EnumerateFileSystemInfos()
    |> Seq.map childFromInfo
    |> List.ofSeq

let existingEntry (fs: IFileSystem) (path: FileSystemPath) =
    let raw = FileSystemPath.value path
    let file = fs.FileInfo.New raw

    if file.Exists then
        Some(entryFromInfo file)
    else
        let directory = fs.DirectoryInfo.New raw

        if directory.Exists then Some(entryFromInfo directory) else None

let read (fs: IFileSystem) (path: FileSystemPath) : Result<Entry list, ListingError> =
    try
        let directory = fs.DirectoryInfo.New(FileSystemPath.value path)

        if not directory.Exists then
            Error(DirectoryNotFound path)
        else
            Ok(directory.EnumerateFileSystemInfos() |> Seq.map entryFromInfo |> List.ofSeq)
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

let parentEntry modified =
    { Name = ParentName
      Kind = Directory
      Size = None
      Measured = None
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None
      Verification = Verified }

let private parentIfAny (fs: IFileSystem) (path: FileSystemPath) =
    fs.DirectoryInfo.New(FileSystemPath.value path).Parent
    |> Option.ofObj
    |> Option.map (fun parent -> DateTimeOffset parent.LastWriteTime |> parentEntry)

let listForDisplay (fs: IFileSystem) (sort: SortOrder) (hidden: HiddenEntries) (path: FileSystemPath) =
    read fs path
    |> Result.map (fun entries ->
        let listed = shown hidden entries

        match parentIfAny fs path with
        | Some parent -> order sort (parent :: listed)
        | None -> order sort listed)
