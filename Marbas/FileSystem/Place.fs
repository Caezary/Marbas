namespace Marbas.FileSystem

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

    let spelled (place: Place) =
        match place with
        | Place.OnDisk path -> [ FileSystemPath.value path ]
        | Place.InArchive(archive, inside) -> [ FileSystemPath.value archive; ArchivePath.value inside ]
