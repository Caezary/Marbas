module Marbas.Ui.PanelTitle

open Marbas.FileSystem

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

let describe (path: FileSystemPath) (sort: SortOrder) (hidden: HiddenEntries) =
    let marks =
        [ keyText sort.Key; directionText sort.Direction ] @ hiddenText hidden

    $"""{FileSystemPath.value path} [{String.concat " " marks}]"""
