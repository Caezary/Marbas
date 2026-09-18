module Marbas.FileSystem.SearchListing

open System
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.DirectoryListing

let private relativeTo (fs: IFileSystem) (root: FileSystemPath) (full: string) =
    let separator = fs.Path.DirectorySeparatorChar
    let bare = (FileSystemPath.value root).TrimEnd separator
    let prefix = bare + string separator

    if full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) then
        full.Substring prefix.Length
    else
        full

let displayName (fs: IFileSystem) (found: FoundEntries) (hit: FindHit) =
    let full = FindHit.spelled fs hit |> FileSystemPath.value

    match found.Ground.Roots with
    | [ single ] -> relativeTo fs (PanelLocation.spelled fs single) full
    | _ -> full
    |> EntryName.create

let private shownAs (fs: IFileSystem) (found: FoundEntries) (hit: FindHit) =
    { hit.Entry with
        Name = displayName fs found hit }

let private surviving (fs: IFileSystem) (found: FoundEntries) =
    found.Hits |> List.filter (FindHit.exists fs)

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
