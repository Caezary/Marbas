module Marbas.Ui.App.CursorInfo

open Marbas.Domain
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui.Format

let private sizeText =
    EntrySize.describe (fun size -> $"{EntrySize.grouped size} bytes")

let describe (entry: Entry option) =
    match entry with
    | None -> ""
    | Some entry -> $"{EntryName.value entry.Name}   {sizeText entry}   {EntryFormat.dateText entry}"
