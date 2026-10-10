module Marbas.Messages.ClipboardErrorMessage

open Marbas.Domain

[<Literal>]
let Title = "Clipboard"

let describe error =
    match error with
    | ClipboardUnavailable -> "Marbas cannot reach the clipboard on this system yet."
    | ClipboardFailed reason -> $"The clipboard could not be used: {reason}"
