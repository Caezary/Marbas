module Marbas.Find.FileSearch

open System
open System.Collections.Immutable
open System.IO
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.Archives
open Marbas.Locations
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

    type private Level =
        { Nth: int
          Latest: DateTimeOffset
          Stored: DateTimeOffset option }

    type private Ending =
        | ReadOn
        | MetLock
        | ToldToStop

    type private Scanned<'found> =
        { Levels: Map<EntryName list, Level>
          Ending: Ending
          Found: 'found }

    let private deepestDirectory (info: ArchiveEntryInfo) =
        if ArchiveEntryInfo.isDirectory info then
            info.Segments.Length
        else
            info.Segments.Length - 1

    let private wantedLevel (scan: Scan) (key: EntryName list) =
        scan.Depth = EveryLevel || key.Length = ArchivePath.depth scan.Level + 1

    let private storedBy (info: ArchiveEntryInfo) (key: EntryName list) =
        if ArchiveEntryInfo.isDirectory info && info.Segments = key then
            Some info.Modified
        else
            None

    let private noteLevel (info: ArchiveEntryInfo) (levels: Map<EntryName list, Level>) key =
        match Map.tryFind key levels, storedBy info key with
        | Some known, None when known.Latest >= info.Modified -> levels
        | Some known, stored ->
            let noted =
                { known with
                    Latest = max known.Latest info.Modified
                    Stored = stored |> Option.orElse known.Stored }

            Map.add key noted levels
        | None, stored ->
            let noted =
                { Nth = levels.Count
                  Latest = info.Modified
                  Stored = stored }

            Map.add key noted levels

    let private note (scan: Scan) (info: ArchiveEntryInfo) (levels: Map<EntryName list, Level>) =
        [ ArchivePath.depth scan.Level + 1 .. deepestDirectory info ]
        |> List.map (fun taken -> List.truncate taken info.Segments)
        |> List.filter (wantedLevel scan)
        |> List.fold (noteLevel info) levels

    let private noted (levels: Map<EntryName list, Level>) =
        levels
        |> Map.toList
        |> List.sortBy (snd >> _.Nth)
        |> List.map (fun (key, level) -> key, level.Stored |> Option.defaultValue level.Latest)

    let private carrying (scanned: Scanned<'found>) (walk: ArchiveWalk<'found>) =
        match walk with
        | KeepReading found -> KeepReading { scanned with Found = found }
        | StopReading found -> StopReading { scanned with Found = found }

    let private stepping (control: BatchControl) scan onFile =
        ArchiveStep<Scanned<'found>>(fun scanned item ->
            let info = item.Info

            if ArchiveEntryInfo.isEncrypted info then
                StopReading { scanned with Ending = MetLock }
            elif control.IsCancelled() then
                StopReading { scanned with Ending = ToldToStop }
            elif ArchivePath.strictlyUnder scan.Level info.Segments then
                let levelled =
                    { scanned with
                        Levels = note scan info scanned.Levels }

                onFile levelled.Found info item.OpenContent |> carrying levelled
            else
                KeepReading scanned)

    let private outcomeOf (walked: ArchiveWalked<Scanned<'found>>) =
        match walked with
        | WalkBroken _ -> Unreadable
        | Walked { Ending = MetLock } -> Locked
        | Walked { Ending = ToldToStop } -> Cancelled
        | Walked { Ending = ReadOn; Levels = levels } -> Levels(noted levels)

    let run (fs: IFileSystem) (control: BatchControl) (scan: Scan) onFile (found: 'found) =
        let walked =
            ArchiveReader.walk
                fs
                None
                scan.Kind
                scan.Archive
                (stepping control scan onFile)
                { Levels = Map.empty
                  Ending = ReadOn
                  Found = found }

        outcomeOf walked, (ArchiveWalked.reached walked).Found

type private Course =
    | Running
    | Halted

type private Walk =
    { Counts: FindCounts
      Visited: ImmutableHashSet<string>
      Course: Course }

let private halted (walk: Walk) = { walk with Course = Halted }

let private skippingOne (walk: Walk) =
    { walk with
        Counts =
            { walk.Counts with
                Skipped = walk.Counts.Skipped + 1 } }

let private onward step (walk: Walk) =
    match walk.Course with
    | Running -> step walk
    | Halted -> walk

let private reading (walk: Walk) =
    match walk.Course with
    | Running -> KeepReading walk
    | Halted -> StopReading walk

let private visited (identity: FileSystemPath) (walk: Walk) =
    walk.Visited.Contains(FileSystemPath.value identity)

let private visiting (identity: FileSystemPath) (walk: Walk) =
    { walk with
        Visited = walk.Visited.Add(FileSystemPath.value identity) }

let private counting (entry: Entry) (counts: FindCounts) =
    match entry.Kind with
    | File -> { counts with Files = counts.Files + 1 }
    | Directory ->
        { counts with
            Directories = counts.Directories + 1 }

let private outcomeOf (walk: Walk) =
    match walk.Course with
    | Running -> Finished walk.Counts
    | Halted -> Stopped walk.Counts

let run (fs: IFileSystem) (control: BatchControl) (report: FindReport -> unit) (query: FindQuery) (ground: FindGround) =
    let platform = FileSystemPath.platformOf fs

    let crosses path =
        ground.OtherRoots
        |> List.exists (fun root -> FileSystemPath.sameEntry platform root path)

    let depth = depthOf query
    let wantsDirectories = FindQuery.targets query = FilesAndDirectories

    let attended step (walk: Walk) =
        if control.IsCancelled() then halted walk else step walk

    let hit location entry (walk: Walk) =
        let counts = counting entry walk.Counts

        report
            { Hit = { Location = location; Entry = entry }
              Counts = counts }

        { walk with Counts = counts }

    let wanted (entry: Entry) =
        HiddenEntries.admits query.Hidden entry.Visibility
        && FindQuery.matchesName query entry.Name

    let byAnswer location entry answer (walk: Walk) =
        match answer with
        | TextMatched -> hit location entry walk
        | TextDidNotMatch -> walk
        | FileSkipped
        | FileUnreadable -> skippingOne walk
        | SearchStopped -> halted walk

    let byNameOrText location entry answered (walk: Walk) =
        match FindQuery.text query with
        | None -> hit location entry walk
        | Some search -> byAnswer location entry (answered search) walk

    let considerFile directory path entry (walk: Walk) =
        byNameOrText (OnDisk directory) entry (fun search -> ContentMatch.search fs control search path) walk

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

    let archiveFile archive (info: ArchiveEntryInfo) openContent (walk: Walk) =
        let entry = ArchiveListing.fileEntry (List.last info.Segments) info

        if wanted entry then
            byNameOrText (InArchive(archive, parentOf info.Segments)) entry (contentOf openContent) walk
        else
            walk

    let archiveFileHit archive level (walk: Walk) (info: ArchiveEntryInfo) openContent =
        if not (ArchiveEntryInfo.isDirectory info) && atSearchedDepth level info then
            archiveFile archive info openContent walk |> reading
        else
            reading walk

    let archiveDirectory archive (segments: EntryName list, modified) (walk: Walk) =
        let entry = ArchiveListing.directoryEntry (List.last segments) modified

        if wanted entry then
            hit (InArchive(archive, parentOf segments)) entry walk
        else
            walk

    let eachDirectory archive candidates (walk: Walk) =
        candidates
        |> List.fold (fun reached candidate -> onward (attended (archiveDirectory archive candidate)) reached) walk

    let scanArchive archive level kind (walk: Walk) =
        control.Report archive
        let names = ArchivePath.segments level

        let scan: ArchiveScan.Scan =
            { Archive = archive
              Kind = kind
              Level = level
              Depth = depth }

        match ArchiveScan.run fs control scan (archiveFileHit archive names) walk with
        | ArchiveScan.Cancelled, found -> halted found
        | ArchiveScan.Locked, found
        | ArchiveScan.Unreadable, found -> skippingOne found
        | ArchiveScan.Levels levels, found when wantsDirectories -> eachDirectory archive levels found
        | ArchiveScan.Levels _, found -> found

    let openArchive path (walk: Walk) =
        match ArchiveReader.kindOf fs path with
        | Some kind -> scanArchive path ArchivePath.root kind walk
        | None -> walk

    let rec descend directory (walk: Walk) =
        control.Report directory

        match Operations.attempting directory (fun () -> children fs directory) with
        | Error _ -> skippingOne walk
        | Ok below ->
            below
            |> List.fold (fun reached child -> onward (attended (consider directory child)) reached) walk

    and consider directory (child: Child) (walk: Walk) =
        let entry = child.Entry

        match HiddenEntries.admits query.Hidden entry.Visibility, entry.Kind with
        | false, _ -> walk
        | true, File -> considerEntry directory child walk |> considerArchive child
        | true, Directory -> considerDirectory directory child walk |> steppingInto child

    and considerEntry directory (child: Child) (walk: Walk) =
        if wanted child.Entry then
            considerFile directory child.Path child.Entry walk
        else
            walk

    and considerArchive (child: Child) (walk: Walk) =
        if looksIntoArchives then
            openArchive child.Path walk
        else
            walk

    and considerDirectory directory (child: Child) (walk: Walk) =
        if wanted child.Entry && wantsDirectories then
            hit (OnDisk directory) child.Entry walk
        else
            walk

    and steppingInto (child: Child) (walk: Walk) =
        let identity = identityOf child.Entry child.Path

        if stepsInto child && not (visited identity walk) then
            attended (descend child.Path) (visiting identity walk)
        else
            walk

    let from place (walk: Walk) =
        match place with
        | OnDisk directory when visited directory walk -> walk
        | OnDisk directory -> attended (descend directory) (visiting directory walk)
        | InArchive(archive, level) ->
            match ArchiveReader.kindOf fs archive with
            | Some kind -> scanArchive archive level kind walk
            | None -> skippingOne walk
        | SearchResults _ -> walk

    let unwalked =
        { Counts =
            { Files = 0
              Directories = 0
              Skipped = 0 }
          Visited = ImmutableHashSet.Create<string>(FileSystemPath.comparer platform)
          Course = Running }

    ground.Roots
    |> List.fold (fun reached place -> onward (from place) reached) unwalked
    |> outcomeOf
