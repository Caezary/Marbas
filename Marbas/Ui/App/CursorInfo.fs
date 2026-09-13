module Marbas.Ui.App.CursorInfo

open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui.Format

let private sizeText =
    EntrySize.describe (fun size -> $"{EntrySize.grouped size} bytes")

let private nameText (entry: Entry) =
    match entry.LinkTarget with
    | Some target -> $"{EntryName.value entry.Name} -> {FileSystemPath.value target}"
    | None -> EntryName.value entry.Name

let describe (entry: Entry option) =
    match entry with
    | None -> ""
    | Some entry -> $"{nameText entry}   {sizeText entry}   {EntryFormat.dateText entry}"
