module Marbas.Ui.PanelFooter

open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State

let private totalBytes (entries: Entry list) =
    entries |> List.sumBy (fun e -> e.Size |> Option.defaultValue 0L)

let private counted noun (entries: Entry list) =
    match entries.Length with
    | 1 -> $"1 {noun}"
    | count -> $"{count} {noun}s"

let private describeContents (entries: Entry list) =
    let listed = entries |> List.filter (fun e -> e.Name <> ParentName)
    let files = listed |> List.filter (fun e -> e.Kind = File)
    let folders = listed |> List.filter (fun e -> e.Kind = Directory)
    let fileCount = counted "file" files
    let folderCount = counted "folder" folders
    let size = EntrySize.grouped (totalBytes files)
    $"{fileCount}, {folderCount}   {size} bytes"

let describe (panel: AppState.PanelState) =
    match panel.Search with
    | Some text -> $"Search: {SearchText.value text}"
    | None ->
        match AppState.selectedEntries panel with
        | [] -> describeContents panel.Entries
        | selected -> SelectionSummary.describe selected
