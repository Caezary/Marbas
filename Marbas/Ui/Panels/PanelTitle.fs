module Marbas.Ui.Panels.PanelTitle

open System.IO.Abstractions
open Marbas.Domain
open Marbas.Locations
open Marbas.Ui.Format

let private keyText key =
    match key with
    | ByName -> "name"
    | ByExtension -> "ext"
    | ByDate -> "date"
    | BySize -> "size"

let private directionText direction =
    match direction with
    | Ascending -> "↑"
    | Descending -> "↓"

let private hiddenText hidden =
    match hidden with
    | Shown -> [ "hidden" ]
    | Concealed -> []

let private findText (fs: IFileSystem) (room: int) (found: FoundEntries) =
    let opening = $"Find: %s{QueryText.describe found.Query} in "

    let where =
        QueryText.where fs found.Ground
        |> PathText.shorten fs.Path.DirectorySeparatorChar (room - opening.Length)

    opening + where

let private whereText (fs: IFileSystem) (room: int) (location: PanelLocation) =
    match location with
    | SearchResults found -> findText fs room found
    | _ ->
        PanelLocation.value fs location
        |> PathText.shorten fs.Path.DirectorySeparatorChar room

let describe (fs: IFileSystem) (width: int) (location: PanelLocation) (sort: SortOrder) (hidden: HiddenEntries) =
    let marks =
        [ keyText sort.Key; directionText sort.Direction ] @ hiddenText hidden
        |> String.concat " "

    let where = whereText fs (width - marks.Length - 3) location

    $"{where} [{marks}]"
