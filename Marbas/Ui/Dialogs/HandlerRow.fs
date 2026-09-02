module Marbas.Ui.Dialogs.HandlerRow

open Marbas.FileSystem

[<Literal>]
let NameWidth = 24

let describe (handler: Handler) =
    let padded = handler.Name.PadRight(NameWidth - 1)
    $"{padded} {ProgramPath.value handler.Program}"
