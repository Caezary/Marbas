module Marbas.Ui.Panels.PanelTitle

open System.IO.Abstractions
open Marbas.FileSystem
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

let describe (fs: IFileSystem) (width: int) (location: PanelLocation) (sort: SortOrder) (hidden: HiddenEntries) =
    let marks =
        [ keyText sort.Key; directionText sort.Direction ] @ hiddenText hidden
        |> String.concat " "

    let where =
        PanelLocation.value fs location
        |> PathText.shorten fs.Path.DirectorySeparatorChar (width - marks.Length - 3)

    $"{where} [{marks}]"
