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

    let watched location =
        match location with
        | OnDisk path -> path
        | InArchive(archive, _) -> archive

    let into (fs: IFileSystem) location (name: Marbas.Domain.EntryName) =
        match location with
        | OnDisk path -> OnDisk(FileSystemPath.combine fs path (Marbas.Domain.EntryName.value name))
        | InArchive(archive, level) -> InArchive(archive, ArchivePath.child level name)

    let combine (fs: IFileSystem) location (name: string) =
        directory location |> Option.map (fun path -> FileSystemPath.combine fs path name)

    let spelled (fs: IFileSystem) location =
        match location with
        | OnDisk path -> path
        | InArchive(archive, level) -> ArchivePath.spelled fs archive level

    let value (fs: IFileSystem) location = spelled fs location |> FileSystemPath.value
