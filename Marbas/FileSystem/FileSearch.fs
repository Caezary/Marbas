module Marbas.FileSystem.FileSearch

open System.Collections.Generic
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.DirectoryListing
open Marbas.FileSystem.Operations

type FindHit =
    { Path: FileSystemPath
      Entry: Entry }

type FindCounts =
    { Files: int
      Directories: int
      Skipped: int }

type FindOutcome =
    | Finished of FindCounts
    | Stopped of FindCounts

let private childrenOf (fs: IFileSystem) (path: FileSystemPath) =
    try
        fs.Directory.EnumerateFileSystemEntries(FileSystemPath.value path)
        |> Seq.map FileSystemPath.create
        |> List.ofSeq
        |> Some
    with _ ->
        None

let private entryOf (fs: IFileSystem) (path: FileSystemPath) =
    try
        match FileSystemPath.kind fs path with
        | DirectoryEntry -> Some(directoryEntry (fs.DirectoryInfo.New(FileSystemPath.value path)))
        | FileEntry -> Some(fileEntry (fs.FileInfo.New(FileSystemPath.value path)))
        | NoEntry -> None
    with _ ->
        None

let private walksDeep query =
    match query.Scope with
    | DirectoryOnly -> false
    | _ -> true

let run (fs: IFileSystem) (control: BatchControl) (report: FindHit -> unit) (query: FindQuery) roots =
    let visited = HashSet<string>()
    let mutable files = 0
    let mutable directories = 0
    let mutable skipped = 0
    let mutable stopped = false

    let counted () =
        { Files = files
          Directories = directories
          Skipped = skipped }

    let hit path entry =
        report { Path = path; Entry = entry }

        match entry.Kind with
        | File -> files <- files + 1
        | Directory -> directories <- directories + 1

    let wanted (entry: Entry) =
        HiddenEntries.admits query.Hidden entry.Visibility
        && FindQuery.matchesName query entry.Name

    let byContent path entry search =
        match ContentMatch.search fs control search path with
        | TextMatched -> hit path entry
        | TextDidNotMatch -> ()
        | FileSkipped
        | FileUnreadable -> skipped <- skipped + 1
        | SearchStopped -> stopped <- true

    let considerFile path entry =
        match FindQuery.text query with
        | None -> hit path entry
        | Some search -> byContent path entry search

    let entered (identity: FileSystemPath) = visited.Add(FileSystemPath.value identity)

    let identityOf (entry: Entry) path = entry.LinkTarget |> Option.defaultValue path

    let follows (entry: Entry) =
        match entry.LinkTarget, query.Links with
        | Some _, StopAtLinks -> false
        | _ -> true

    let rec walk path =
        if control.IsCancelled() then
            stopped <- true
        else
            control.Report path

            match childrenOf fs path with
            | None -> skipped <- skipped + 1
            | Some children -> each children

    and each children =
        match children with
        | [] -> ()
        | child :: rest ->
            if control.IsCancelled() then
                stopped <- true
            else
                consider child

                if not stopped then
                    each rest

    and consider child =
        match entryOf fs child with
        | None -> skipped <- skipped + 1
        | Some entry when not (HiddenEntries.admits query.Hidden entry.Visibility) -> ()
        | Some entry ->
            match entry.Kind with
            | File when wanted entry -> considerFile child entry
            | File -> ()
            | Directory ->
                if wanted entry && FindQuery.targets query = FilesAndDirectories then
                    hit child entry

                if walksDeep query && follows entry && entered (identityOf entry child) then
                    walk child

    let rec start places =
        match places with
        | [] -> ()
        | place :: rest ->
            if entered place then
                walk place

            if not stopped then
                start rest

    start roots

    if stopped then Stopped(counted ()) else Finished(counted ())
