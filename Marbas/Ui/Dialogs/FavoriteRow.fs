module Marbas.Ui.Dialogs.FavoriteRow

open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Format

[<Literal>]
let private DigitGap = "  "

let describe (fs: IFileSystem) (width: int) (slot: FavoriteSlot) (place: Place option) =
    let digit = string (FavoriteSlot.value slot)

    match place with
    | None -> digit
    | Some held ->
        let lead = digit + DigitGap

        let path =
            HistoryRow.place fs held
            |> PathText.shorten fs.Path.DirectorySeparatorChar (width - lead.Length)

        lead + path
