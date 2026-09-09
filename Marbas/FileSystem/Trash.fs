module Marbas.FileSystem.Trash

open System
open System.Globalization
open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Domain
open Marbas.FileSystem.Operations

[<Literal>]
let TrashDirectory = "Trash"

[<Literal>]
let FilesDirectory = "files"

[<Literal>]
let InfoDirectory = "info"

[<Literal>]
let InfoSuffix = ".trashinfo"

[<Literal>]
let InfoHeader = "[Trash Info]"

[<Literal>]
let private LocalDirectory = ".local"

[<Literal>]
let private ShareDirectory = "share"

[<Literal>]
let private DeletionDateFormat = "yyyy-MM-ddTHH:mm:ss"

let location (fs: IFileSystem) (dataHome: string) (userProfile: string) =
    let home =
        if String.IsNullOrWhiteSpace dataHome then
            fs.Path.Combine(userProfile, LocalDirectory, ShareDirectory)
        else
            dataHome

    fs.Path.Combine(home, TrashDirectory) |> FileSystemPath.create

let private storedFiles fs trash = FileSystemPath.combine fs trash FilesDirectory

let private storedInfo fs trash = FileSystemPath.combine fs trash InfoDirectory

let private guard (path: FileSystemPath) action =
    try
        action ()
        Ok()
    with ex ->
        Error(mapException path ex)

let private ensureDirectory (fs: IFileSystem) (path: FileSystemPath) =
    guard path (fun () -> fs.Directory.CreateDirectory(FileSystemPath.value path) |> ignore)

let private taken (fs: IFileSystem) trash (name: string) =
    FileSystemPath.kind fs (FileSystemPath.combine fs (storedFiles fs trash) name) <> NoEntry
    || FileSystemPath.kind fs (FileSystemPath.combine fs (storedInfo fs trash) (name + InfoSuffix)) <> NoEntry

let private numbered (fs: IFileSystem) (name: string) ordinal =
    let stem = fs.Path.GetFileNameWithoutExtension name
    let extension = fs.Path.GetExtension name
    $"{stem}_{ordinal}{extension}"

let private freeName (fs: IFileSystem) trash (name: EntryName) =
    let rec pick ordinal =
        let candidate =
            if ordinal = 1 then
                EntryName.value name
            else
                numbered fs (EntryName.value name) ordinal

        if taken fs trash candidate then pick (ordinal + 1) else candidate

    pick 1

let private encode (fs: IFileSystem) (raw: string) =
    let separator = fs.Path.DirectorySeparatorChar

    raw.Split separator
    |> Array.map Uri.EscapeDataString
    |> String.concat (string separator)

let private describe (fs: IFileSystem) (source: FileSystemPath) (deletedAt: DateTimeOffset) =
    [ InfoHeader
      $"Path={encode fs (FileSystemPath.value source)}"
      $"DeletionDate={deletedAt.ToString(DeletionDateFormat, CultureInfo.InvariantCulture)}"
      "" ]
    |> String.concat "\n"

let private writeInfo (fs: IFileSystem) (path: FileSystemPath) content =
    guard path (fun () -> fs.File.WriteAllText(FileSystemPath.value path, (content: string)))

let private discard (fs: IFileSystem) (path: FileSystemPath) =
    try
        fs.File.Delete(FileSystemPath.value path)
    with _ ->
        ()

let send (fs: IFileSystem) (trash: FileSystemPath) (now: unit -> DateTimeOffset) (source: FileSystemPath) =
    match FileSystemPath.kind fs source with
    | NoEntry -> Error(SourceNotFound source)
    | _ ->
        result {
            do! ensureDirectory fs (storedFiles fs trash)
            do! ensureDirectory fs (storedInfo fs trash)

            let name = freeName fs trash (FileSystemPath.name fs source)
            let info = FileSystemPath.combine fs (storedInfo fs trash) (name + InfoSuffix)

            do! writeInfo fs info (describe fs source (now ()))

            return!
                FileSystemPath.combine fs (storedFiles fs trash) name
                |> moveTo fs unattended source
                |> Result.teeError (fun _ -> discard fs info)
        }
