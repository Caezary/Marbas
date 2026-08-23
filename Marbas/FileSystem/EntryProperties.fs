namespace Marbas.FileSystem

open System
open System.IO
open System.IO.Abstractions

type EntryFlag =
    | ReadOnlyFlag
    | HiddenFlag
    | SystemFlag
    | ArchiveFlag

type EntryProperties =
    { Path: FileSystemPath
      Flags: Set<EntryFlag>
      Created: DateTime
      Modified: DateTime
      Accessed: DateTime }

module EntryProperties =

    let flags = [ ReadOnlyFlag; HiddenFlag; SystemFlag; ArchiveFlag ]

    let label flag =
        match flag with
        | ReadOnlyFlag -> "Read-only"
        | HiddenFlag -> "Hidden"
        | SystemFlag -> "System"
        | ArchiveFlag -> "Archive"

    let private attribute flag =
        match flag with
        | ReadOnlyFlag -> FileAttributes.ReadOnly
        | HiddenFlag -> FileAttributes.Hidden
        | SystemFlag -> FileAttributes.System
        | ArchiveFlag -> FileAttributes.Archive

    let private flagsOf (attributes: FileAttributes) =
        flags |> List.filter (attribute >> attributes.HasFlag) |> Set.ofList

    let private attributesOf (existing: FileAttributes) (set: Set<EntryFlag>) =
        flags
        |> List.fold
            (fun attributes flag ->
                if Set.contains flag set then
                    attributes ||| attribute flag
                else
                    attributes &&& ~~~(attribute flag))
            existing

    let has flag (properties: EntryProperties) = Set.contains flag properties.Flags

    let setFlag flag wanted (properties: EntryProperties) =
        { properties with
            Flags =
                if wanted then
                    Set.add flag properties.Flags
                else
                    Set.remove flag properties.Flags }

    let private info (fs: IFileSystem) (path: FileSystemPath) : IFileSystemInfo option =
        let raw = FileSystemPath.value path

        match FileSystemPath.kind fs path with
        | DirectoryEntry -> Some(fs.DirectoryInfo.New raw)
        | FileEntry -> Some(fs.FileInfo.New raw)
        | NoEntry -> None

    let read (fs: IFileSystem) (path: FileSystemPath) =
        match info fs path with
        | None -> Error(Operations.SourceNotFound path)
        | Some entry ->
            try
                Ok
                    { Path = path
                      Flags = flagsOf entry.Attributes
                      Created = entry.CreationTime
                      Modified = entry.LastWriteTime
                      Accessed = entry.LastAccessTime }
            with ex ->
                Error(Operations.mapException path ex)

    let write (fs: IFileSystem) (properties: EntryProperties) =
        match info fs properties.Path with
        | None -> Error(Operations.SourceNotFound properties.Path)
        | Some entry ->
            try
                entry.CreationTime <- properties.Created
                entry.LastWriteTime <- properties.Modified
                entry.LastAccessTime <- properties.Accessed
                entry.Attributes <- attributesOf entry.Attributes properties.Flags
                Ok()
            with ex ->
                Error(Operations.mapException properties.Path ex)
