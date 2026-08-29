module Marbas.FileSystem.ArchiveListing

open System
open System.IO
open System.IO.Abstractions
open SharpCompress.Archives
open SharpCompress.Readers
open Marbas.FileSystem.DirectoryListing

type ArchiveError =
    | KindCannotBeRead of path: FileSystemPath
    | ArchiveNotFound of path: FileSystemPath
    | PasswordNeeded of path: FileSystemPath
    | ArchiveDamaged of path: FileSystemPath * message: string

type private Item =
    { Segments: EntryName list
      IsDirectory: bool
      Size: int64
      Modified: DateTimeOffset }

let mapException path (ex: exn) =
    match ex with
    | :? FileNotFoundException
    | :? DirectoryNotFoundException -> ArchiveNotFound path
    | :? SharpCompress.Common.CryptographicException
    | :? Security.Cryptography.CryptographicException -> PasswordNeeded path
    | _ -> ArchiveDamaged(path, ex.Message)

let private segmentsOf (key: string) =
    key.Split '/'
    |> List.ofArray
    |> List.filter (fun segment -> segment <> "" && segment <> ".")
    |> List.map EntryName.create

let private itemOf (entry: SharpCompress.Common.IEntry) =
    { Segments = segmentsOf (Option.ofObj entry.Key |> Option.defaultValue "")
      IsDirectory = entry.IsDirectory
      Size = entry.Size
      Modified =
        entry.LastModifiedTime
        |> Option.ofNullable
        |> Option.map DateTimeOffset
        |> Option.defaultValue DateTimeOffset.MinValue }

let private options () = ReaderOptions(LeaveStreamOpen = true)

let private wholeArchive (stream: Stream) =
    use archive = ArchiveFactory.OpenArchive(stream, options ())

    archive.Entries |> Seq.map itemOf |> List.ofSeq

let private streamedEntries (stream: Stream) =
    use reader = ReaderFactory.OpenReader(stream, options ())

    [ while reader.MoveToNextEntry() do
          itemOf reader.Entry ]

let private itemsOf kind =
    match kind with
    | Tar
    | TarGz
    | TarBz2
    | TarXz -> streamedEntries
    | Zip
    | SevenZip
    | Rar -> wholeArchive

let private below (level: EntryName list) (item: Item) =
    item.Segments.Length > level.Length
    && List.truncate level.Length item.Segments = level

let private fileEntry (name: EntryName) (item: Item) =
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

let private storedAt depth (group: Item list) =
    group |> List.tryFind (fun item -> item.Segments.Length = depth + 1)

let private entryOf depth (name: EntryName) (group: Item list) =
    match storedAt depth group with
    | Some item when not item.IsDirectory && group.Length = 1 -> fileEntry name item
    | Some item when item.IsDirectory -> directoryEntry name item.Modified
    | _ -> directoryEntry name (group |> List.map _.Modified |> List.max)

let private atLevel (level: ArchivePath) (items: Item list) =
    let names = ArchivePath.segments level

    items
    |> List.filter (below names)
    |> List.groupBy (fun item -> item.Segments |> List.item names.Length)
    |> List.map (fun (name, group) -> entryOf names.Length name group)

let private readable (fs: IFileSystem) (path: FileSystemPath) =
    FileSystemPath.name fs path
    |> ArchiveKind.recognise
    |> Option.filter (ArchiveKind.capabilities >> _.CanRead)

let read (fs: IFileSystem) (path: FileSystemPath) (level: ArchivePath) : Result<Entry list, ArchiveError> =
    match readable fs path with
    | None -> Error(KindCannotBeRead path)
    | Some kind ->
        try
            use stream = fs.File.OpenRead(FileSystemPath.value path)
            Ok(itemsOf kind stream |> atLevel level)
        with ex ->
            Error(mapException path ex)
