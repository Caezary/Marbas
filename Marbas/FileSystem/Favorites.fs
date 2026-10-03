namespace Marbas.FileSystem

open System
open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Domain

type FavoriteChange = Keep of FavoriteSlot * Place

type Favorites = { Places: Map<FavoriteSlot, Place> }

module Favorites =

    [<Literal>]
    let private FileName = "favorites.conf"

    [<Literal>]
    let private SlotPrefix = "favorite"

    [<Literal>]
    let private InsidePart = "inside"

    let empty = { Places = Map.empty }

    let beside (fs: IFileSystem) (settingsPath: FileSystemPath) =
        FileSystemPath.combine fs (FileSystemPath.directory fs settingsPath) FileName

    let placeIn slot (favorites: Favorites) = Map.tryFind slot favorites.Places

    let private breaksALine (text: string) = text.IndexOfAny [| '\n'; '\r' |] >= 0

    let private writable place =
        Place.spelled place |> List.exists breaksALine |> not

    let apply change (favorites: Favorites) =
        match change with
        | Keep(slot, place) when writable place ->
            { favorites with
                Places = Map.add slot place favorites.Places }
        | Keep _ -> favorites

    let private slotKey slot =
        $"{SlotPrefix}.{FavoriteSlot.value slot}"

    let private slotLines (slot, place) =
        let key = slotKey slot

        match place with
        | Place.OnDisk path -> [ $"{key}={FileSystemPath.value path}" ]
        | Place.InArchive(archive, inside) ->
            [ $"{key}={FileSystemPath.value archive}"
              $"{key}.{InsidePart}={ArchivePath.value inside}" ]

    let format (favorites: Favorites) =
        favorites.Places |> Map.toList |> List.collect slotLines

    let private keyAndValue (line: string) =
        match line.Split('=', 2) with
        | [| key; value |] -> Some(key.Trim().ToLowerInvariant(), value)
        | _ -> None

    let private placeFrom (read: Map<string, string>) slot =
        let key = slotKey slot

        Map.tryFind key read
        |> Option.filter (String.IsNullOrEmpty >> not)
        |> Option.bind (fun value ->
            let path = FileSystemPath.create value

            match Map.tryFind $"{key}.{InsidePart}" read with
            | None -> Some(Place.OnDisk path)
            | Some inside -> ArchivePath.tryCreate inside |> Option.map (fun level -> Place.InArchive(path, level)))

    let parse (lines: string list) =
        let read = lines |> List.choose keyAndValue |> Map.ofList

        { Places =
            FavoriteSlot.all
            |> List.choose (fun slot -> placeFrom read slot |> Option.map (fun place -> slot, place))
            |> Map.ofList }

    let load (fs: IFileSystem) (path: FileSystemPath) =
        let file = FileSystemPath.value path

        try
            if fs.File.Exists file then
                fs.File.ReadAllLines file |> List.ofArray |> parse |> Ok
            else
                Ok empty
        with ex ->
            Error(FavoritesError.mapException path ex)

    let private save (fs: IFileSystem) (path: FileSystemPath) favorites =
        try
            fs.Directory.CreateDirectory(FileSystemPath.value (FileSystemPath.directory fs path))
            |> ignore

            fs.File.WriteAllLines(FileSystemPath.value path, format favorites)
            Ok()
        with ex ->
            Error(FavoritesError.mapException path ex)

    let change (fs: IFileSystem) (path: FileSystemPath) change =
        result {
            let! current = load fs path
            let changed = apply change current
            do! save fs path changed
            return changed
        }
