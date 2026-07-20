module Marbas.Ui.SelectionSummary

open Marbas.FileSystem.DirectoryListing

let describe (entries: Entry list) =
    match entries with
    | [] -> ""
    | _ ->
        let total = entries |> List.sumBy (fun e -> e.Size |> Option.defaultValue 0L)
        $"{entries.Length} selected   {EntrySize.grouped total} bytes"
