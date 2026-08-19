module Marbas.Ui.PanelFooter

open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State

let private totalBytes (entries: Entry list) =
    entries |> List.sumBy (fun e -> e.Size |> Option.defaultValue 0L)

let private describeContents (entries: Entry list) =
    let files = entries |> List.filter (fun e -> e.Kind = File)
    $"{files.Length} files   {EntrySize.grouped (totalBytes files)} bytes"

let describe (panel: AppState.PanelState) =
    match panel.Search with
    | Some text -> $"Search: {SearchText.value text}"
    | None ->
        match AppState.selectedEntries panel with
        | [] -> describeContents panel.Entries
        | selected -> SelectionSummary.describe selected
