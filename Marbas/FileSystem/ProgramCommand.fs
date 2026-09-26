module Marbas.FileSystem.ProgramCommand

open System
open Marbas.Domain

[<Literal>]
let private Quote = '"'

let private runnable text =
    Handler.fileName text
    |> EntryName.tryCreate
    |> Option.map (fun name -> Executability.ofName name = Executable)
    |> Option.defaultValue false

let private quoted (text: string) =
    match text.IndexOf(Quote, 1) with
    | -1 -> text.TrimStart Quote
    | closing -> text.Substring(1, closing - 1)

let private firstWord (text: string) =
    match text.IndexOf ' ' with
    | -1 -> text
    | space -> text.Substring(0, space)

let private unquoted (text: string) =
    let words = text.Split ' '

    [ for count in 1 .. words.Length -> String.Join(' ', Array.take count words) ]
    |> List.tryFind runnable
    |> Option.defaultValue (firstWord text)

let programOf (command: string) =
    match command.Trim() with
    | "" -> None
    | text when text.StartsWith Quote -> ProgramPath.tryCreate (quoted text)
    | text -> ProgramPath.tryCreate (unquoted text)
