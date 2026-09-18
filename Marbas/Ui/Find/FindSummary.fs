module Marbas.Ui.Find.FindSummary

open Marbas.FileSystem.FileSearch

type SearchProgress =
    | Walking of directory: string * found: FindCounts
    | Done of FindOutcome

[<Literal>]
let private Separator = " — "

let private files count =
    if count = 1 then "1 file" else $"%d{count} files"

let private directories count =
    if count = 1 then "1 directory" else $"%d{count} directories"

let private skipped count =
    if count = 1 then "1 skipped" else $"%d{count} skipped"

let private tally counts =
    let found =
        if counts.Files = 0 && counts.Directories = 0 then
            [ "nothing found" ]
        else
            [ files counts.Files; directories counts.Directories ]

    let passed =
        if counts.Skipped = 0 then [] else [ skipped counts.Skipped ]

    found @ passed |> String.concat ", "

let describe progress =
    match progress with
    | Walking(directory, counts) -> $"Searching: %s{directory}%s{Separator}%s{tally counts}"
    | Done(Finished counts) -> $"Finished%s{Separator}%s{tally counts}"
    | Done(Stopped counts) -> $"Stopped%s{Separator}%s{tally counts}"
