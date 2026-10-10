module Marbas.State.Messages.DeleteConfirmation

open Marbas.Domain
open Marbas.State.Update

[<Literal>]
let Title = "Delete"

[<Literal>]
let PermanentTitle = "Delete for good"

[<Literal>]
let WipeTitle = "Wipe"

[<Literal>]
let NonEmptyWarning = "Directories are deleted with everything inside them."

[<Literal>]
let TrashWarning = "Directories go to the trash with everything inside them."

[<Literal>]
let WipeContentsWarning = "Directories are wiped with everything inside them."

[<Literal>]
let WipeExplanation =
    "Each file is overwritten with random bytes before it is deleted."

[<Literal>]
let WipeLimits =
    "On a solid-state disk, or a copy-on-write or journaling\nfile system, the old blocks may outlive the file anyway."

let title (request: DeleteRequest) =
    match request.Mode with
    | ToTrash -> Title
    | Permanently -> PermanentTitle
    | ByWiping -> WipeTitle

let private warning (request: DeleteRequest) =
    match request.Mode with
    | ToTrash -> TrashWarning
    | Permanently -> NonEmptyWarning
    | ByWiping -> WipeContentsWarning

let private caveats (request: DeleteRequest) =
    match request.Mode with
    | ByWiping -> [ WipeExplanation; WipeLimits ]
    | ToTrash
    | Permanently -> []

let describe (request: DeleteRequest) =
    let question =
        match request.Mode, request.Targets with
        | ToTrash, [ single ] -> $"Move '{EntryName.value single}' to the trash?"
        | ToTrash, many -> $"Move {List.length many} entries to the trash?"
        | Permanently, [ single ] -> $"Delete '{EntryName.value single}' for good?"
        | Permanently, many -> $"Delete {List.length many} entries for good?"
        | ByWiping, [ single ] -> $"Wipe '{EntryName.value single}'?"
        | ByWiping, many -> $"Wipe {List.length many} entries?"

    let scoped =
        match request.Scope with
        | WithDirectoryContents -> [ warning request ]
        | NamedEntriesOnly -> []

    question :: scoped @ caveats request |> String.concat "\n"
