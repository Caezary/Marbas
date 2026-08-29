module Marbas.FileSystem.ArchiveListing

open System
open System.IO
open System.IO.Abstractions
open Marbas.FileSystem.DirectoryListing

let private below (level: EntryName list) (item: ArchiveItem) =
    item.Segments.Length > level.Length
    && List.truncate level.Length item.Segments = level

let private fileEntry (name: EntryName) (item: ArchiveItem) =
    { Name = name
      Kind = File
      Size = Some item.Size
      Modified = item.Modified
      Visibility = EntryVisibility.classify name FileAttributes.None
      Executability = Executability.classify name UnixFileMode.None }

let private directoryEntry (name: EntryName) (modified: DateTimeOffset) =
    { Name = name
      Kind = Directory
      Size = None
      Modified = modified
      Visibility = EntryVisibility.classify name FileAttributes.None
      Executability = NotExecutable }

let private storedAt depth (group: ArchiveItem list) =
    group |> List.tryFind (fun item -> item.Segments.Length = depth + 1)

let private entryOf depth (name: EntryName) (group: ArchiveItem list) =
    match storedAt depth group with
    | Some item when not item.IsDirectory && group.Length = 1 -> fileEntry name item
    | Some item when item.IsDirectory -> directoryEntry name item.Modified
    | _ -> directoryEntry name (group |> List.map _.Modified |> List.max)

let private atLevel (level: ArchivePath) (items: ArchiveItem list) =
    let names = ArchivePath.segments level

    items
    |> List.filter (below names)
    |> List.groupBy (fun item -> item.Segments |> List.item names.Length)
    |> List.map (fun (name, group) -> entryOf names.Length name group)

let read (fs: IFileSystem) (path: FileSystemPath) (level: ArchivePath) : Result<Entry list, ArchiveError> =
    match ArchiveReader.kindOf fs path with
    | None -> Error(KindCannotBeRead path)
    | Some kind ->
        try
            Ok(ArchiveReader.items fs None kind path |> List.ofSeq |> atLevel level)
        with ex ->
            Error(ArchiveReader.mapException path ex)
