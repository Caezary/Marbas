module Marbas.FileSystem.FileSearch

open System
open System.Collections.Generic
open System.IO
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.DirectoryListing
open Marbas.FileSystem.Operations

type FindCounts =
    { Files: int
      Directories: int
      Skipped: int }

type FindOutcome =
    | Finished of FindCounts
    | Stopped of FindCounts

type FindReport = { Hit: FindHit; Counts: FindCounts }

type private WalkDepth =
    | ThisLevelOnly
    | EveryLevel

let private depthOf query =
    match query.Scope with
    | DirectoryOnly
    | PathDirectories -> ThisLevelOnly
    | _ -> EveryLevel

let private pathOf segments =
    segments |> List.fold ArchivePath.child ArchivePath.root

let private parentOf segments =
    segments |> List.take (List.length segments - 1) |> pathOf

module private ArchiveScan =

    type Scan =
        { Archive: FileSystemPath
          Kind: ArchiveKind
          Level: ArchivePath
          Depth: WalkDepth }

    type Outcome =
        | Levels of (EntryName list * DateTimeOffset) list
        | Locked
        | Cancelled
        | Unreadable

    type private Times = Dictionary<EntryName list, DateTimeOffset>

    let private deepestDirectory (info: ArchiveEntryInfo) =
        if ArchiveEntryInfo.isDirectory info then
            info.Segments.Length
        else
            info.Segments.Length - 1

    let private wantedLevel (scan: Scan) (key: EntryName list) =
        scan.Depth = EveryLevel || key.Length = ArchivePath.depth scan.Level + 1

    let private note (scan: Scan) (seen: Times) (stored: Times) (info: ArchiveEntryInfo) =
        for taken in ArchivePath.depth scan.Level + 1 .. deepestDirectory info do
            let key = List.truncate taken info.Segments

            if wantedLevel scan key then
                match seen.TryGetValue key with
                | true, known when known >= info.Modified -> ()
                | _ -> seen[key] <- info.Modified

                if ArchiveEntryInfo.isDirectory info && info.Segments = key then
                    stored[key] <- info.Modified

    let private noted (seen: Times) (stored: Times) =
        seen
        |> Seq.map (fun level ->
            match stored.TryGetValue level.Key with
            | true, modified -> level.Key, modified
            | _ -> level.Key, level.Value)
        |> List.ofSeq

    let private stepping (control: BatchControl) scan seen stored (locked: bool ref) (cancelled: bool ref) onFile =
        ArchiveStep(fun item ->
            let info = item.Info

            if ArchiveEntryInfo.isEncrypted info then
                locked.Value <- true
                StopReading
            elif control.IsCancelled() then
                cancelled.Value <- true
                StopReading
            elif ArchivePath.strictlyUnder scan.Level info.Segments then
                note scan seen stored info
                onFile info item.OpenContent
            else
                KeepReading)

    let run (fs: IFileSystem) (control: BatchControl) (scan: Scan) onFile =
        let seen = Times()
        let stored = Times()
        let locked = ref false
        let cancelled = ref false

        try
            ArchiveReader.withItems
                fs
                None
                scan.Kind
                scan.Archive
                (stepping control scan seen stored locked cancelled onFile)

            if locked.Value then Locked
            elif cancelled.Value then Cancelled
            else Levels(noted seen stored)
        with _ ->
            Unreadable

let run (fs: IFileSystem) (control: BatchControl) (report: FindReport -> unit) (query: FindQuery) (ground: FindGround) =
    let platform = FileSystemPath.platformOf fs
    let visited = HashSet<string>(FileSystemPath.comparer platform)

    let crosses path =
        ground.OtherRoots
        |> List.exists (fun root -> FileSystemPath.sameEntry platform root path)

    let depth = depthOf query
    let wantsDirectories = FindQuery.targets query = FilesAndDirectories
    let mutable files = 0
    let mutable directories = 0
    let mutable skipped = 0
    let mutable stopped = false

    let counted () =
        { Files = files
          Directories = directories
          Skipped = skipped }

    let hit location entry =
        match entry.Kind with
        | File -> files <- files + 1
        | Directory -> directories <- directories + 1

        report
            { Hit = { Location = location; Entry = entry }
              Counts = counted () }

    let wanted (entry: Entry) =
        HiddenEntries.admits query.Hidden entry.Visibility
        && FindQuery.matchesName query entry.Name

    let byAnswer location entry answer =
        match answer with
        | TextMatched -> hit location entry
        | TextDidNotMatch -> ()
        | FileSkipped
        | FileUnreadable -> skipped <- skipped + 1
        | SearchStopped -> stopped <- true

    let considerFile directory path entry =
        match FindQuery.text query with
        | None -> hit (OnDisk directory) entry
        | Some search -> byAnswer (OnDisk directory) entry (ContentMatch.search fs control search path)

    let firstVisit (identity: FileSystemPath) =
        visited.Add(FileSystemPath.value identity)

    let identityOf (entry: Entry) path =
        entry.LinkTarget |> Option.defaultValue path

    let follows (entry: Entry) =
        match entry.LinkTarget, query.Links with
        | Some _, StopAtLinks -> false
        | _ -> true

    let stepsInto (child: Child) =
        depth = EveryLevel && follows child.Entry && not (crosses child.Path)

    let looksIntoArchives = depth = EveryLevel && query.Archives = SearchArchives

    let contentOf (openContent: unit -> Stream) search =
        use content = openContent ()
        ContentMatch.searchStream control search content

    let atSearchedDepth (level: EntryName list) (info: ArchiveEntryInfo) =
        depth = EveryLevel || info.Segments.Length = level.Length + 1

    let archiveFileHit archive level (info: ArchiveEntryInfo) openContent =
        if not (ArchiveEntryInfo.isDirectory info) && atSearchedDepth level info then
            let entry = ArchiveListing.fileEntry (List.last info.Segments) info
            let location = InArchive(archive, parentOf info.Segments)

            if wanted entry then
                match FindQuery.text query with
                | None -> hit location entry
                | Some search -> byAnswer location entry (contentOf openContent search)

        if stopped then StopReading else KeepReading

    let rec eachDirectory archive candidates =
        match candidates with
        | [] -> ()
        | (segments: EntryName list, modified) :: rest ->
            if control.IsCancelled() then
                stopped <- true
            else
                let entry = ArchiveListing.directoryEntry (List.last segments) modified

                if wanted entry then
                    hit (InArchive(archive, parentOf segments)) entry

                if not stopped then
                    eachDirectory archive rest

    let scanArchive archive level kind =
        control.Report archive
        let names = ArchivePath.segments level

        let scan: ArchiveScan.Scan =
            { Archive = archive
              Kind = kind
              Level = level
              Depth = depth }

        match ArchiveScan.run fs control scan (archiveFileHit archive names) with
        | ArchiveScan.Cancelled -> stopped <- true
        | ArchiveScan.Locked
        | ArchiveScan.Unreadable -> skipped <- skipped + 1
        | ArchiveScan.Levels found ->
            if wantsDirectories && not stopped then
                eachDirectory archive found

    let openArchive path =
        match ArchiveReader.kindOf fs path with
        | Some kind -> scanArchive path ArchivePath.root kind
        | None -> ()

    let rec walk directory =
        if control.IsCancelled() then
            stopped <- true
        else
            control.Report directory

            match Operations.attempting directory (fun () -> children fs directory) with
            | Error _ -> skipped <- skipped + 1
            | Ok below -> each directory below

    and each directory below =
        match below with
        | [] -> ()
        | child :: rest ->
            if control.IsCancelled() then
                stopped <- true
            else
                consider directory child

                if not stopped then
                    each directory rest

    and consider directory (child: Child) =
        let entry = child.Entry

        if HiddenEntries.admits query.Hidden entry.Visibility then
            match entry.Kind with
            | File ->
                if wanted entry then
                    considerFile directory child.Path entry

                if looksIntoArchives then
                    openArchive child.Path
            | Directory ->
                if wanted entry && wantsDirectories then
                    hit (OnDisk directory) entry

                if stepsInto child && firstVisit (identityOf entry child.Path) then
                    walk child.Path

    let rec start places =
        match places with
        | [] -> ()
        | place :: rest ->
            match place with
            | OnDisk directory ->
                if firstVisit directory then
                    walk directory
            | InArchive(archive, level) ->
                match ArchiveReader.kindOf fs archive with
                | Some kind -> scanArchive archive level kind
                | None -> skipped <- skipped + 1
            | SearchResults _ -> ()

            if not stopped then
                start rest

    start ground.Roots

    if stopped then
        Stopped(counted ())
    else
        Finished(counted ())
