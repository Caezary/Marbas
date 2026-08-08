module Marbas.Ui.DeleteConfirmation

[<Literal>]
let Title = "Delete"

[<Literal>]
let NonEmptyWarning = "Directories are deleted with everything inside them."

let describe (names: string list) nonEmptyDirectory =
    let question =
        match names with
        | [ single ] -> $"Delete '{single}'?"
        | many -> $"Delete {List.length many} entries?"

    if nonEmptyDirectory then
        $"{question}\n{NonEmptyWarning}"
    else
        question
