module Marbas.Locations.SearchListing

open System
open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing

type ResultsListing =
    { Listed: Entry array
      Refused: ArchiveError list }

type private Survival =
    | Standing
    | Gone
    | Unconfirmed of ArchiveError

let private shownAs (fs: IFileSystem) (found: FoundEntries) (hit: FindHit) =
    { hit.Entry with
        Name = FindHit.displayName fs found.Ground hit }

let private prefixes (segments: EntryName list) =
    [ 1 .. segments.Length ]
    |> List.map (fun length -> List.truncate length segments)

let private indexedPrefixes (fs: IFileSystem) (archive: FileSystemPath) =
    ArchiveIndex.read fs archive
    |> Result.map (Seq.collect (_.Segments >> prefixes) >> Set.ofSeq)

let private survivalIn (held: Result<Set<EntryName list>, ArchiveError>) segments =
    match held with
    | Ok indexed when indexed.Contains segments -> Standing
    | Ok _ -> Gone
    | Error error -> Unconfirmed error

type private IndexedArchives = Map<FileSystemPath, Result<Set<EntryName list>, ArchiveError>>

let private heldIn (fs: IFileSystem) (archives: IndexedArchives) archive =
    match Map.tryFind archive archives with
    | Some known -> known, archives
    | None ->
        let read = indexedPrefixes fs archive
        read, Map.add archive read archives

let private survival (fs: IFileSystem) (archives: IndexedArchives) (hit: FindHit) =
    match hit.Location with
    | _ when not (FindHit.exists fs hit) -> (hit, Gone), archives
    | InArchive(archive, level) ->
        let held, known = heldIn fs archives archive
        (hit, survivalIn held (ArchivePath.segments level @ [ hit.Entry.Name ])), known
    | OnDisk _
    | SearchResults _ -> (hit, Standing), archives

let private survivals (fs: IFileSystem) (found: FoundEntries) =
    found.Hits |> List.mapFold (survival fs) Map.empty |> fst

let hitFor (found: FoundEntries) (name: EntryName) = found.Shown |> Map.tryFind name

let private resultsParent (fs: IFileSystem) (found: FoundEntries) =
    let holding = PanelLocation.holding fs found.Origin

    let modified =
        try
            DateTimeOffset(fs.DirectoryInfo.New(FileSystemPath.value holding).LastWriteTime)
        with _ ->
            DateTimeOffset.MinValue

    parentEntry modified

let private strictly (hit, survival) =
    match survival with
    | Standing -> Ok(Some hit)
    | Gone -> Ok None
    | Unconfirmed error -> Error error

let read (fs: IFileSystem) (found: FoundEntries) =
    survivals fs found
    |> List.traverseResultM strictly
    |> Result.map (List.choose id >> List.map (shownAs fs found))

let private leniently (fs: IFileSystem) (found: FoundEntries) (hit, survival) =
    match survival with
    | Standing -> Some(shownAs fs found hit)
    | Gone -> None
    | Unconfirmed _ ->
        Some
            { shownAs fs found hit with
                Verification = Unverified }

let private refusal (_, survival) =
    match survival with
    | Unconfirmed error -> Some error
    | Standing
    | Gone -> None

let listForDisplay (fs: IFileSystem) (sort: SortOrder) (hidden: HiddenEntries) (found: FoundEntries) =
    let survivals = survivals fs found
    let entries = survivals |> List.choose (leniently fs found)

    { Listed = order sort (resultsParent fs found :: shown hidden entries)
      Refused = survivals |> List.choose refusal |> List.distinct }
