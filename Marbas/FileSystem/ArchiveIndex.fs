module Marbas.FileSystem.ArchiveIndex

open System
open System.IO.Abstractions
open System.Runtime.CompilerServices
open Marbas.Domain

[<Literal>]
let private Remembered = 4

type private Stamp = { Length: int64; Modified: DateTimeOffset }

type private Indexed =
    { Path: FileSystemPath
      Stamp: Stamp
      Items: ArchiveEntryInfo list }

let private remembered = ConditionalWeakTable<IFileSystem, ResizeArray<Indexed>>()

let private cacheOf (fs: IFileSystem) =
    remembered.GetValue(fs, fun _ -> ResizeArray<Indexed>())

let private stampOf (fs: IFileSystem) (path: FileSystemPath) =
    let file = fs.FileInfo.New(FileSystemPath.value path)

    { Length = file.Length
      Modified = DateTimeOffset file.LastWriteTimeUtc }

let private scan (fs: IFileSystem) kind path =
    let items = ResizeArray<ArchiveEntryInfo>()

    ArchiveReader.withItems
        fs
        None
        kind
        path
        (ArchiveStep(fun item ->
            items.Add item.Info
            KeepReading))

    List.ofSeq items

let private keep (cache: ResizeArray<Indexed>) indexed =
    cache.RemoveAll(fun held -> held.Path = indexed.Path) |> ignore
    cache.Insert(0, indexed)

    if cache.Count > Remembered then
        cache.RemoveAt(cache.Count - 1)

let private known (cache: ResizeArray<Indexed>) path stamp =
    cache |> Seq.tryFind (fun held -> held.Path = path && held.Stamp = stamp)

let private answer (fs: IFileSystem) kind path (cache: ResizeArray<Indexed>) =
    let stamp = stampOf fs path

    match known cache path stamp with
    | Some held -> held.Items
    | None ->
        let items = scan fs kind path
        keep cache { Path = path; Stamp = stamp; Items = items }
        items

let read (fs: IFileSystem) (path: FileSystemPath) : Result<ArchiveEntryInfo list, ArchiveError> =
    match ArchiveReader.kindOf fs path with
    | None -> Error(KindCannotBeRead path)
    | Some kind ->
        let cache = cacheOf fs

        try
            Ok(lock cache (fun () -> answer fs kind path cache))
        with ex ->
            Error(ArchiveReader.mapException path ex)
