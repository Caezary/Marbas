module Marbas.Ui.PatternDialog

open Terminal.Gui.Views

[<Literal>]
let DefaultPattern = "*"

let create title = PromptDialog.create title "Pattern:" DefaultPattern

let field (dialog: Dialog) = PromptDialog.field dialog

let pattern (dialog: Dialog) = PromptDialog.text dialog
