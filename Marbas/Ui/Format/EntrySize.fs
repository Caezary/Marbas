module Marbas.Ui.Format.EntrySize

open System.Globalization
open Marbas.Domain
open Marbas.FileSystem.DirectoryListing

[<Literal>]
let DirectoryMark = "<DIR>"

[<Literal>]
let AtLeastMark = "≥"

let describe (formatBytes: int64 -> string) entry =
    match entry.Kind, entry.Measured with
    | Directory, Some(Exact counted) -> formatBytes counted
    | Directory, Some(AtLeast counted) -> AtLeastMark + formatBytes counted
    | Directory, None -> DirectoryMark
    | File, _ -> entry.Size |> Option.map formatBytes |> Option.defaultValue ""

let plain (size: int64) = string size

let grouped (size: int64) =
    size.ToString("N0", CultureInfo.InvariantCulture)
