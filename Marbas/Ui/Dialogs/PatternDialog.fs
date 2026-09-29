module Marbas.Ui.Dialogs.PatternDialog

open Terminal.Gui.Views

[<Literal>]
let DefaultPattern = "*"

let create remembered title =
    PromptDialog.offering remembered title "Pattern:" DefaultPattern

let field (dialog: Dialog) = PromptDialog.field dialog
