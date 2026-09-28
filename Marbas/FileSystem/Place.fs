namespace Marbas.FileSystem

open System.IO.Abstractions

[<RequireQualifiedAccess>]
type Place =
    | OnDisk of path: FileSystemPath
    | InArchive of archive: FileSystemPath * inside: ArchivePath

module Place =

    let same platform (one: Place) (other: Place) =
        match one, other with
        | Place.OnDisk one, Place.OnDisk other -> FileSystemPath.sameEntry platform one other
        | Place.InArchive(one, inside), Place.InArchive(other, otherInside) ->
            FileSystemPath.sameEntry platform one other && inside = otherInside
        | _ -> false

    let ofLocation location =
        match location with
        | OnDisk path -> Some(Place.OnDisk path)
        | InArchive(archive, level) -> Some(Place.InArchive(archive, level))
        | SearchResults _ -> None

    let toLocation place =
        match place with
        | Place.OnDisk path -> OnDisk path
        | Place.InArchive(archive, inside) -> InArchive(archive, inside)

    let beside (fs: IFileSystem) place =
        match place with
        | Place.OnDisk path -> Some(OnDisk(FileSystemPath.directory fs path), FileSystemPath.name fs path)
        | Place.InArchive(archive, inside) ->
            ArchivePath.name inside
            |> Option.map (fun name ->
                InArchive(archive, ArchivePath.parent inside |> Option.defaultValue ArchivePath.root), name)

    let spelled (place: Place) =
        match place with
        | Place.OnDisk path -> [ FileSystemPath.value path ]
        | Place.InArchive(archive, inside) -> [ FileSystemPath.value archive; ArchivePath.value inside ]
