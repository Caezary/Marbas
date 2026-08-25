module Marbas.Ui.Format.EntrySize

open System.Globalization
open Marbas.FileSystem.DirectoryListing

[<Literal>]
let DirectoryMark = "<DIR>"

let describe (formatBytes: int64 -> string) entry =
    match entry.Kind with
    | Directory -> DirectoryMark
    | File -> entry.Size |> Option.map formatBytes |> Option.defaultValue ""

let plain (size: int64) = string size

let grouped (size: int64) =
    size.ToString("N0", CultureInfo.InvariantCulture)
