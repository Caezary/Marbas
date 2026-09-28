module Marbas.Ui.Dialogs.HistoryRow

open System
open System.Globalization
open System.IO.Abstractions
open Marbas.FileSystem

[<Literal>]
let TimePattern = "HH:mm"

[<Literal>]
let DatePattern = "yyyy-MM-dd"

let moment (now: DateTimeOffset) (at: DateTimeOffset) =
    let seen = at.ToOffset now.Offset
    let pattern = if seen.Date = now.Date then TimePattern else DatePattern
    seen.ToString(pattern, CultureInfo.InvariantCulture)

let place (fs: IFileSystem) (place: Place) =
    match place with
    | Place.OnDisk path -> FileSystemPath.value path
    | Place.InArchive(archive, inside) -> ArchivePath.spelled fs archive inside |> FileSystemPath.value
