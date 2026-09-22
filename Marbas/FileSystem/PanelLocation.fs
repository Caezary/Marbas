namespace Marbas.FileSystem

open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.DirectoryListing

type PanelLocation =
    | OnDisk of directory: FileSystemPath
    | InArchive of archive: FileSystemPath * level: ArchivePath
    | SearchResults of found: FoundEntries

and FindHit = { Location: PanelLocation; Entry: Entry }

and FindGround =
    { Roots: PanelLocation list
      OtherRoots: FileSystemPath list }

and FoundEntries =
    { Query: FindQuery
      Ground: FindGround
      Hits: FindHit list
      Shown: Map<EntryName, FindHit>
      Origin: PanelLocation
      Returning: EntryName option }

module PanelLocation =

    let directory location =
        match location with
        | OnDisk path -> Some path
        | InArchive _
        | SearchResults _ -> None

    let rec origin location =
        match location with
        | SearchResults found -> origin found.Origin
        | _ -> location

    let rec holding (fs: IFileSystem) location =
        match location with
        | OnDisk path -> path
        | InArchive(archive, _) -> FileSystemPath.directory fs archive
        | SearchResults found -> holding fs found.Origin

    let watched location =
        match location with
        | OnDisk path -> Some path
        | InArchive(archive, _) -> Some archive
        | SearchResults _ -> None

    let into (fs: IFileSystem) location (name: EntryName) =
        match location with
        | OnDisk path -> Some(OnDisk(FileSystemPath.combine fs path (EntryName.value name)))
        | InArchive(archive, level) -> Some(InArchive(archive, ArchivePath.child level name))
        | SearchResults _ -> None

    let combine (fs: IFileSystem) location (name: string) =
        directory location |> Option.map (fun path -> FileSystemPath.combine fs path name)

    let rec spelled (fs: IFileSystem) location =
        match location with
        | OnDisk path -> path
        | InArchive(archive, level) -> ArchivePath.spelled fs archive level
        | SearchResults found -> spelled fs found.Origin

    let value (fs: IFileSystem) location = spelled fs location |> FileSystemPath.value

module FindHit =

    let spelled (fs: IFileSystem) hit =
        match hit.Location with
        | OnDisk directory -> FileSystemPath.combine fs directory (EntryName.value hit.Entry.Name)
        | InArchive(archive, level) -> ArchivePath.spelled fs archive (ArchivePath.child level hit.Entry.Name)
        | SearchResults _ -> PanelLocation.spelled fs hit.Location

    let private stands (fs: IFileSystem) (path: FileSystemPath) =
        let spelled = FileSystemPath.value path

        fs.File.Exists spelled || fs.Directory.Exists spelled

    let exists (fs: IFileSystem) hit =
        match hit.Location with
        | InArchive(archive, _) -> stands fs archive
        | _ -> stands fs (spelled fs hit)

    let private under (fs: IFileSystem) (root: FileSystemPath) (full: FileSystemPath) =
        FileSystemPath.pathAfter (FileSystemPath.platformOf fs) root full
        |> Option.defaultValue (FileSystemPath.value full)

    let displayName (fs: IFileSystem) (ground: FindGround) (hit: FindHit) =
        let full = spelled fs hit

        match ground.Roots with
        | [ single ] -> under fs (PanelLocation.spelled fs single) full
        | _ -> FileSystemPath.value full
        |> EntryName.create

module FoundEntries =

    let shownBy (fs: IFileSystem) (ground: FindGround) (hits: FindHit list) =
        hits
        |> List.map (fun hit -> FindHit.displayName fs ground hit, hit)
        |> Map.ofList
