module Marbas.Ui.Dialogs.HistoryRow

open System
open System.Globalization
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem

[<Literal>]
let TimePattern = "HH:mm"

[<Literal>]
let DatePattern = "yyyy-MM-dd"

let moment (now: DateTimeOffset) (at: DateTimeOffset) =
    let seen = at.ToOffset now.Offset
    let pattern = if seen.Date = now.Date then TimePattern else DatePattern
    seen.ToString(pattern, CultureInfo.InvariantCulture)

[<Literal>]
let AdministratorNote = "admin"

let private programName program =
    (ProgramPath.value program).Split([| '\\'; '/' |]) |> Array.last

let launchWhat (request: LaunchRequest) =
    let target = FileSystemPath.value request.Target

    match request.Program with
    | Some program -> $"{target} ({programName program})"
    | None -> target

let launchHow (request: LaunchRequest) =
    match request.Elevation with
    | AsUser -> ""
    | AsAdministrator -> AdministratorNote

[<Literal>]
let ViewedMark = "V"

[<Literal>]
let EditedMark = "E"

let visitHow how =
    match how with
    | Viewed -> ViewedMark
    | Edited -> EditedMark

let place (fs: IFileSystem) (place: Place) =
    match place with
    | Place.OnDisk path -> FileSystemPath.value path
    | Place.InArchive(archive, inside) -> ArchivePath.spelled fs archive inside |> FileSystemPath.value
