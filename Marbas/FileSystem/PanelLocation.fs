namespace Marbas.FileSystem

open System.IO.Abstractions

type PanelLocation =
    | OnDisk of directory: FileSystemPath
    | InArchive of archive: FileSystemPath * level: ArchivePath

module PanelLocation =

    let directory location =
        match location with
        | OnDisk path -> Some path
        | InArchive _ -> None

    let holding (fs: IFileSystem) location =
        match location with
        | OnDisk path -> path
        | InArchive(archive, _) -> FileSystemPath.directory fs archive

    let combine (fs: IFileSystem) location (name: string) =
        directory location |> Option.map (fun path -> FileSystemPath.combine fs path name)

    let value (fs: IFileSystem) location =
        match location with
        | OnDisk path -> FileSystemPath.value path
        | InArchive(archive, level) ->
            ArchivePath.segments level
            |> List.fold (fun path name -> FileSystemPath.combine fs path (EntryName.value name)) archive
            |> FileSystemPath.value
