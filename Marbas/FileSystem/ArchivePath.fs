namespace Marbas.FileSystem

open System

type ArchivePath = private ArchivePath of EntryName list

module ArchivePath =

    [<Literal>]
    let private Separator = '/'

    let root = ArchivePath []

    let segments (ArchivePath names) = names

    let isRoot path = segments path |> List.isEmpty

    let value path =
        segments path |> List.map EntryName.value |> String.concat (string Separator)

    let private names (raw: string) =
        raw.Split Separator |> List.ofArray

    let private climbs (segment: string) =
        segment = "." || segment = ".."

    let private legal (segment: string) =
        not (String.IsNullOrWhiteSpace segment) && not (climbs segment)

    let tryCreate (raw: string) =
        match raw with
        | "" -> Some root
        | _ when names raw |> List.forall legal -> Some(ArchivePath(names raw |> List.map EntryName.create))
        | _ -> None

    let child path (name: EntryName) =
        ArchivePath(segments path @ [ name ])

    let name path = segments path |> List.tryLast

    let parent path =
        match segments path |> List.rev with
        | [] -> None
        | _ :: above -> Some(ArchivePath(List.rev above))
