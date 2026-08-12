module Marbas.Ui.DeleteConfirmation

open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update

[<Literal>]
let Title = "Delete"

[<Literal>]
let NonEmptyWarning = "Directories are deleted with everything inside them."

let describe (request: DeleteRequest) =
    let question =
        match request.Targets with
        | [ single ] -> $"Delete '{EntryName.value single}'?"
        | many -> $"Delete {List.length many} entries?"

    match request.Scope with
    | WithDirectoryContents -> $"{question}\n{NonEmptyWarning}"
    | NamedEntriesOnly -> question
