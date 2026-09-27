module Marbas.Ui.Panels.PanelFooter

open Marbas.Domain
open Marbas.FileSystem.DirectoryListing
open Marbas.State
open Marbas.Ui.Format

let private totalBytes (entries: Entry array) =
    entries |> Array.sumBy (fun e -> e.Size |> Option.defaultValue 0L)

let private counted noun (entries: Entry array) =
    match entries.Length with
    | 1 -> $"1 {noun}"
    | count -> $"{count} {noun}s"

let private unverifiedCount (entries: Entry array) =
    match entries |> Array.filter (fun e -> e.Verification = Unverified) with
    | [||] -> ""
    | unverified -> $", {unverified.Length} unverified"

let private describeContents (entries: Entry array) =
    let listed = entries |> Array.filter (fun e -> e.Name <> ParentName)
    let files = listed |> Array.filter (fun e -> e.Kind = File)
    let folders = listed |> Array.filter (fun e -> e.Kind = Directory)
    let fileCount = counted "file" files
    let folderCount = counted "folder" folders
    let unverified = unverifiedCount listed
    let size = EntrySize.grouped (totalBytes files)
    $"{fileCount}, {folderCount}{unverified}   {size} bytes"

let describe (panel: PanelState) =
    match panel.Search with
    | Some text -> $"Search: {SearchText.value text}"
    | None ->
        match PanelState.selectedEntries panel with
        | [] -> describeContents panel.Entries
        | selected -> SelectionSummary.describe selected
