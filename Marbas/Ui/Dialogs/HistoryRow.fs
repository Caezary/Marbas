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
let AdministratorNote = "as administrator"

let private programName program =
    (ProgramPath.value program).Split([| '\\'; '/' |]) |> Array.last

let launchHow (request: LaunchRequest) =
    [ request.Program |> Option.map programName
      match request.Elevation with
      | AsUser -> None
      | AsAdministrator -> Some AdministratorNote ]
    |> List.choose id
    |> String.concat ", "

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
