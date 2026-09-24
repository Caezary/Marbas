module Marbas.Ui.Dialogs.PatternDialog

open Terminal.Gui.Views
open Marbas.Domain

[<Literal>]
let DefaultPattern = "*"

let create title = PromptDialog.create title "Pattern:" DefaultPattern

let field (dialog: Dialog) = PromptDialog.field dialog

let pattern (dialog: Dialog) =
    PromptDialog.text dialog |> Option.bind NamePattern.tryCreate
