module Marbas.FileSystem.ArchiveListing

open System
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.DirectoryListing

let fileEntry (name: EntryName) (info: ArchiveEntryInfo) =
    { Name = name
      Kind = File
      Size = Some info.Size
      Measured = None
      Modified = info.Modified
      Visibility = EntryVisibility.ofName name
      Executability = Executability.ofName name
      LinkTarget = None }

let directoryEntry (name: EntryName) (modified: DateTimeOffset) =
    { Name = name
      Kind = Directory
      Size = None
      Measured = None
      Modified = modified
      Visibility = EntryVisibility.ofName name
      Executability = NotExecutable
      LinkTarget = None }

let private storedAt depth (group: ArchiveEntryInfo list) =
    group |> List.tryFind (fun info -> info.Segments.Length = depth + 1)

let private entryOf depth (name: EntryName) (group: ArchiveEntryInfo list) =
    match storedAt depth group with
    | Some info when ArchiveEntryInfo.isDirectory info -> directoryEntry name info.Modified
    | Some info when group.Length = 1 -> fileEntry name info
    | _ -> directoryEntry name (group |> List.map _.Modified |> List.max)

let private atLevel (level: ArchivePath) (items: ArchiveEntryInfo list) =
    let names = ArchivePath.segments level

    items
    |> List.filter (fun info -> ArchivePath.strictlyUnder level info.Segments)
    |> List.groupBy (fun info -> info.Segments |> List.item names.Length)
    |> List.map (fun (name, group) -> entryOf names.Length name group)

let read (fs: IFileSystem) (path: FileSystemPath) (level: ArchivePath) : Result<Entry list, ArchiveError> =
    ArchiveIndex.read fs path |> Result.map (atLevel level)

let private archiveParent (fs: IFileSystem) (path: FileSystemPath) =
    DateTimeOffset(fs.FileInfo.New(FileSystemPath.value path).LastWriteTime) |> parentEntry

let listForDisplay (fs: IFileSystem) (sort: SortOrder) (hidden: HiddenEntries) path level =
    read fs path level
    |> Result.mapError ArchiveUnreadable
    |> Result.map (fun entries -> order sort (archiveParent fs path :: shown hidden entries))
