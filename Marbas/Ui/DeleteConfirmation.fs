module Marbas.Ui.DeleteConfirmation

open Marbas.FileSystem

[<Literal>]
let Title = "Delete"

[<Literal>]
let NonEmptyWarning = "Directories are deleted with everything inside them."

let describe (names: EntryName list) nonEmptyDirectory =
    let question =
        match names with
        | [ single ] -> $"Delete '{EntryName.value single}'?"
        | many -> $"Delete {List.length many} entries?"

    if nonEmptyDirectory then
        $"{question}\n{NonEmptyWarning}"
    else
        question
