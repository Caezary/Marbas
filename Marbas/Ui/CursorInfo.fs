module Marbas.Ui.CursorInfo

open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing

let private sizeText =
    EntrySize.describe (fun size -> $"{EntrySize.grouped size} bytes")

let describe (entry: Entry option) =
    match entry with
    | None -> ""
    | Some entry -> $"{EntryName.value entry.Name}   {sizeText entry}   {EntryFormat.dateText entry}"
