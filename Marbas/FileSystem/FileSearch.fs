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
    | DirectoryOnly
    | PathDirectories -> false
    | _ -> true

let private pathOf segments =
    segments |> List.fold ArchivePath.child ArchivePath.root

let private below (level: EntryName list) (item: ArchiveItem) =
    item.Segments.Length > level.Length
    && List.truncate level.Length item.Segments = level

let private deepestDirectory (item: ArchiveItem) =
    if item.IsDirectory then
        item.Segments.Length
    else
        item.Segments.Length - 1

let private parentOf segments =
    segments |> List.take (List.length segments - 1) |> pathOf

let run (fs: IFileSystem) (control: BatchControl) (report: FindHit -> unit) (query: FindQuery) (ground: FindGround) =
    let platform = FileSystemPath.platformOf fs
    let visited = HashSet<string>(FileSystemPath.comparer platform)

    let crosses path =
        ground.OtherRoots |> List.exists (fun root -> FileSystemPath.sameEntry platform root path)
    let deep = walksDeep query
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
        report { Location = location; Entry = entry }

        match entry.Kind with
        | File -> files <- files + 1
        | Directory -> directories <- directories + 1

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

    let entered (identity: FileSystemPath) = visited.Add(FileSystemPath.value identity)

    let identityOf (entry: Entry) path = entry.LinkTarget |> Option.defaultValue path

    let follows (entry: Entry) =
        match entry.LinkTarget, query.Links with
        | Some _, StopAtLinks -> false
        | _ -> true

    let contentOf (item: ArchiveItem) search =
        use buffer = new MemoryStream()
        item.WriteTo buffer
        buffer.Position <- 0L
        ContentMatch.inStream control search buffer

    let fileInside archive (level: EntryName list) (item: ArchiveItem) =
        if not item.IsDirectory && (deep || item.Segments.Length = level.Length + 1) then
            let entry = ArchiveListing.fileEntry (List.last item.Segments) item
            let location = InArchive(archive, parentOf item.Segments)

            if wanted entry then
                match FindQuery.text query with
                | None -> hit location entry
                | Some search -> byAnswer location entry (contentOf item search)

    let noteLevels (levels: Dictionary<EntryName list, DateTimeOffset>) stored (item: ArchiveItem) (level: EntryName list) =
        for depth in level.Length + 1 .. deepestDirectory item do
            let key = List.truncate depth item.Segments

            if deep || key.Length = level.Length + 1 then
                match levels.TryGetValue key with
                | true, known when known >= item.Modified -> ()
                | _ -> levels[key] <- item.Modified

                if item.IsDirectory && item.Segments = key then
                    (stored: Dictionary<EntryName list, DateTimeOffset>)[key] <- item.Modified

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
        let levels = Dictionary<EntryName list, DateTimeOffset>()
        let stored = Dictionary<EntryName list, DateTimeOffset>()
        let mutable locked = false
        let mutable running = true

        try
            use items = (ArchiveReader.items fs None kind archive).GetEnumerator()

            while running && items.MoveNext() do
                let item = items.Current

                if item.IsEncrypted then
                    locked <- true
                    running <- false
                elif control.IsCancelled() then
                    stopped <- true
                    running <- false
                else
                    if below names item then
                        noteLevels levels stored item names
                        fileInside archive names item

                    running <- not stopped

            if locked then
                skipped <- skipped + 1
            elif wantsDirectories && not stopped then
                levels
                |> Seq.map (fun level ->
                    match stored.TryGetValue level.Key with
                    | true, modified -> level.Key, modified
                    | _ -> level.Key, level.Value)
                |> List.ofSeq
                |> eachDirectory archive
        with _ ->
            skipped <- skipped + 1

    let openArchive path =
        match ArchiveReader.kindOf fs path with
        | Some kind -> scanArchive path ArchivePath.root kind
        | None -> ()

    let rec walk directory =
        if control.IsCancelled() then
            stopped <- true
        else
            control.Report directory

            match childrenOf fs directory with
            | None -> skipped <- skipped + 1
            | Some children -> each directory children

    and each directory children =
        match children with
        | [] -> ()
        | child :: rest ->
            if control.IsCancelled() then
                stopped <- true
            else
                consider directory child

                if not stopped then
                    each directory rest

    and consider directory child =
        match entryOf fs child with
        | None -> skipped <- skipped + 1
        | Some entry when not (HiddenEntries.admits query.Hidden entry.Visibility) -> ()
        | Some entry ->
            match entry.Kind with
            | File ->
                if wanted entry then
                    considerFile directory child entry

                if deep && query.Archives = SearchArchives then
                    openArchive child
            | Directory ->
                if wanted entry && wantsDirectories then
                    hit (OnDisk directory) entry

                if deep && follows entry && not (crosses child) && entered (identityOf entry child) then
                    walk child

    let rec start places =
        match places with
        | [] -> ()
        | place :: rest ->
            match place with
            | OnDisk directory ->
                if entered directory then
                    walk directory
            | InArchive(archive, level) ->
                match ArchiveReader.kindOf fs archive with
                | Some kind -> scanArchive archive level kind
                | None -> skipped <- skipped + 1
            | SearchResults _ -> ()

            if not stopped then
                start rest

    start ground.Roots

    if stopped then Stopped(counted ()) else Finished(counted ())
