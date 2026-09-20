module Marbas.FileSystem.SearchListing

open System
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.DirectoryListing

let private relativeTo (fs: IFileSystem) (root: FileSystemPath) (full: FileSystemPath) =
    FileSystemPath.pathAfter (FileSystemPath.platformOf fs) root full
    |> Option.defaultValue (FileSystemPath.value full)

let displayName (fs: IFileSystem) (found: FoundEntries) (hit: FindHit) =
    let full = FindHit.spelled fs hit

    match found.Ground.Roots with
    | [ single ] -> relativeTo fs (PanelLocation.spelled fs single) full
    | _ -> FileSystemPath.value full
    |> EntryName.create

let private shownAs (fs: IFileSystem) (found: FoundEntries) (hit: FindHit) =
    { hit.Entry with
        Name = displayName fs found hit }

let private prefixes (segments: EntryName list) =
    [ 1 .. segments.Length ] |> List.map (fun length -> List.truncate length segments)

let private heldIn (fs: IFileSystem) (archive: FileSystemPath) =
    ArchiveReader.kindOf fs archive
    |> Option.bind (fun kind ->
        try
            ArchiveReader.items fs None kind archive
            |> Seq.collect (_.Segments >> prefixes)
            |> Set.ofSeq
            |> Some
        with _ ->
            None)

let private surviving (fs: IFileSystem) (found: FoundEntries) =
    let archives = Collections.Generic.Dictionary<FileSystemPath, Set<EntryName list> option>()

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
            match held archive with
            | Some names -> names.Contains(ArchivePath.segments level @ [ hit.Entry.Name ])
            | None -> true
        | _ -> FindHit.exists fs hit

    found.Hits |> List.filter survives

let hitFor (fs: IFileSystem) (found: FoundEntries) (name: EntryName) =
    found.Hits |> List.tryFind (fun hit -> displayName fs found hit = name)

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
    surviving fs found |> List.map (shownAs fs found)

let listForDisplay (fs: IFileSystem) (sort: SortOrder) (hidden: HiddenEntries) (found: FoundEntries) =
    Ok(order sort (parentEntry fs found :: shown hidden (read fs found)))
