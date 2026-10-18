module Marbas.Messages.ExpansionRefusalMessage

open Marbas.Domain
open Marbas.FileSystem
open Marbas.Programs

[<Literal>]
let Title = "Cannot run command"

let private sideName side =
    match side with
    | Left -> "left"
    | Right -> "right"

let describe refusal =
    match refusal with
    | InsideArchive place ->
        $"'{FileSystemPath.value place}' is inside an archive and cannot be handed to a program. Unpack it first."
    | NoEntryChosen side ->
        $"Nothing is chosen in the {sideName side} panel: the cursor stands on '..' and nothing is marked."
    | NoDirectory side -> $"The {sideName side} panel shows search results and stands in no directory."
    | NoProgram -> "The command line names no program to start."
