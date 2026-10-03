module Marbas.Ui.Messages.ClipboardErrorMessage

open Marbas.Domain

[<Literal>]
let Title = "Clipboard"

let describe error =
    match error with
    | ClipboardUnavailable -> "Marbas cannot reach the clipboard on this system yet."
    | ClipboardFailed reason -> $"Could not put the text on the clipboard: {reason}"
