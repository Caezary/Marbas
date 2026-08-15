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

let describe (path: FileSystemPath) (sort: SortOrder) =
    $"{FileSystemPath.value path} [{keyText sort.Key} {directionText sort.Direction}]"
