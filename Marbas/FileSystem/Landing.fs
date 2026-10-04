namespace Marbas.FileSystem

open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.DirectoryListing

type Landing =
    | Into of PanelLocation
    | Beside of PanelLocation * EntryName

module Landing =

    let rec private nearestStanding (fs: IFileSystem) (path: FileSystemPath) (below: EntryName list) =
        match FileSystemPath.kind fs path, FileSystemPath.parent fs path with
        | NoEntry, Some parent -> nearestStanding fs parent (FileSystemPath.name fs path :: below)
        | NoEntry, None -> None
        | kind, _ -> Some(path, kind, below)

    let private landingOn archive level (entry: Entry) =
        match entry.Kind with
        | Directory -> Into(InArchive(archive, ArchivePath.child level entry.Name))
        | File -> Beside(InArchive(archive, level), entry.Name)

    let private inArchive (fs: IFileSystem) archive (segments: EntryName list) =
        match List.rev segments with
        | [] -> None
        | name :: above ->
            let level = above |> List.rev |> List.fold ArchivePath.child ArchivePath.root

            ArchiveListing.read fs archive level
            |> Result.toOption
            |> Option.bind (List.tryFind (fun entry -> entry.Name = name))
            |> Option.map (landingOn archive level)

    let private isReadableArchive (fs: IFileSystem) path =
        ArchiveReader.kindOf fs path |> Option.isSome

    let ofPath (fs: IFileSystem) (path: FileSystemPath) =
        match nearestStanding fs path [] with
        | Some(found, DirectoryEntry, []) -> Some(Into(OnDisk found))
        | Some(found, FileEntry, []) ->
            Some(Beside(OnDisk(FileSystemPath.directory fs found), FileSystemPath.name fs found))
        | Some(archive, FileEntry, below) when isReadableArchive fs archive -> inArchive fs archive below
        | _ -> None
