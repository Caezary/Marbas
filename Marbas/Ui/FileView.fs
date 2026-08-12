module Marbas.Ui.FileView

open Marbas.FileSystem.FileContent

[<Literal>]
let TruncatedNotice = "--- shown up to the first megabyte ---"

let lines (content: Content) =
    let body =
        match content.Body with
        | Text lines -> lines
        | Binary bytes -> HexDump.describe bytes

    match content.Extent with
    | Whole -> body
    | Truncated -> body @ [ TruncatedNotice ]
