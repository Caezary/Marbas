module Marbas.FileSystem.SearchListing

open System
open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Domain
open Marbas.FileSystem.DirectoryListing

let private shownAs (fs: IFileSystem) (found: FoundEntries) (hit: FindHit) =
    { hit.Entry with
        Name = FindHit.displayName fs found.Ground hit }

let private prefixes (segments: EntryName list) =
    [ 1 .. segments.Length ] |> List.map (fun length -> List.truncate length segments)

let private heldIn (fs: IFileSystem) (archive: FileSystemPath) =
    ArchiveIndex.read fs archive
    |> Result.map (Seq.collect (_.Segments >> prefixes) >> Set.ofSeq)

let private surviving (fs: IFileSystem) (found: FoundEntries) =
    let archives =
        Collections.Generic.Dictionary<FileSystemPath, Result<Set<EntryName list>, ArchiveError>>()

    let held archive =
        match archives.TryGetValue archive with
        | true, known -> known
        | _ ->
            let read = heldIn fs archive
            archives[archive] <- read
            read

    let survives (hit: FindHit) =
        match hit.Location with
        | InArchive(archive, level) when FindHit.exists fs hit ->
            held archive
            |> Result.map (fun names -> names.Contains(ArchivePath.segments level @ [ hit.Entry.Name ]))
        | _ -> Ok(FindHit.exists fs hit)

    found.Hits
    |> List.traverseResultM (fun hit -> survives hit |> Result.map (fun kept -> hit, kept))
    |> Result.map (List.filter snd >> List.map fst)

let hitFor (found: FoundEntries) (name: EntryName) = found.Shown |> Map.tryFind name

let private parentEntry (fs: IFileSystem) (found: FoundEntries) =
    let holding = PanelLocation.holding fs found.Origin

    let modified =
        try
            DateTimeOffset(fs.DirectoryInfo.New(FileSystemPath.value holding).LastWriteTime)
        with _ ->
            DateTimeOffset.MinValue

    { Name = ParentName
      Kind = Directory
      Size = None
      Measured = None
      Modified = modified
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None }

let read (fs: IFileSystem) (found: FoundEntries) =
    surviving fs found |> Result.map (List.map (shownAs fs found))

let listForDisplay (fs: IFileSystem) (sort: SortOrder) (hidden: HiddenEntries) (found: FoundEntries) =
    read fs found
    |> Result.mapError ArchiveUnreadable
    |> Result.map (fun entries -> order sort (parentEntry fs found :: shown hidden entries))
