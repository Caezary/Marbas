module Marbas.Ui.Messages.DeleteConfirmation

open Marbas.Domain
open Marbas.State.Update

[<Literal>]
let Title = "Delete"

[<Literal>]
let PermanentTitle = "Delete for good"

[<Literal>]
let NonEmptyWarning = "Directories are deleted with everything inside them."

[<Literal>]
let TrashWarning = "Directories go to the trash with everything inside them."

let title (request: DeleteRequest) =
    match request.Mode with
    | ToTrash -> Title
    | Permanently -> PermanentTitle

let private warning (request: DeleteRequest) =
    match request.Mode with
    | ToTrash -> TrashWarning
    | Permanently -> NonEmptyWarning

let describe (request: DeleteRequest) =
    let question =
        match request.Mode, request.Targets with
        | ToTrash, [ single ] -> $"Move '{EntryName.value single}' to the trash?"
        | ToTrash, many -> $"Move {List.length many} entries to the trash?"
        | Permanently, [ single ] -> $"Delete '{EntryName.value single}' for good?"
        | Permanently, many -> $"Delete {List.length many} entries for good?"

    match request.Scope with
    | WithDirectoryContents -> $"{question}\n{warning request}"
    | NamedEntriesOnly -> question
