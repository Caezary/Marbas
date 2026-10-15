module Marbas.FileSystem.MimeDatabase

open System
open Marbas.Domain

[<Literal>]
let GlobsFile = "globs"

[<Literal>]
let ApplicationsFile = "mimeapps.list"

[<Literal>]
let private DefaultSection = "[Default Applications]"

[<Literal>]
let private AddedSection = "[Added Associations]"

[<Literal>]
let private RemovedSection = "[Removed Associations]"

let private meaningful (lines: string list) =
    lines
    |> List.map _.Trim()
    |> List.filter (fun line -> line <> "" && not (line.StartsWith '#'))

let private globEntry (line: string) =
    match line.Split ':' with
    | [| kind; glob |] -> Some(kind, glob)
    | [| _; kind; glob |] -> Some(kind, glob)
    | _ -> None

let private globExtension (glob: string) =
    if glob.StartsWith("*.", StringComparison.Ordinal) then
        FileExtension.tryCreate (glob.Substring 2)
    else
        None

let typesFor extension lines =
    meaningful lines
    |> List.choose globEntry
    |> List.filter (fun (_, glob) -> globExtension glob = Some extension)
    |> List.map fst
    |> List.distinct

let private sectioned lines =
    meaningful lines
    |> List.fold
        (fun (section, gathered) (line: string) ->
            if line.StartsWith '[' then
                line, gathered
            else
                section, (section, line) :: gathered)
        ("", [])
    |> snd
    |> List.rev

let private listed section mimeType lines =
    sectioned lines
    |> List.filter (fun (named, _) -> named = section)
    |> List.collect (fun (_, line: string) ->
        match line.Split('=', 2) with
        | [| key; value |] when key.Trim() = mimeType -> value.Split ';' |> List.ofArray
        | _ -> [])
    |> List.map _.Trim()
    |> List.filter (fun entry -> entry <> "")

let applicationsFor mimeType lines =
    let removed = listed RemovedSection mimeType lines |> Set.ofList

    listed DefaultSection mimeType lines @ listed AddedSection mimeType lines
    |> List.distinct
    |> List.filter (removed.Contains >> not)
