module Marbas.FileSystem.ProgramCommand

open System
open System.IO

[<Literal>]
let private Quote = '"'

let private separators = [| '\\'; '/' |]

let private runnable (text: string) =
    text.Split separators
    |> Array.last
    |> EntryName.tryCreate
    |> Option.map (fun name -> Executability.classify name UnixFileMode.None = Executable)
    |> Option.defaultValue false

let private quoted (text: string) =
    match text.IndexOf(Quote, 1) with
    | -1 -> text.TrimStart Quote
    | closing -> text.Substring(1, closing - 1)

let private unquoted (text: string) =
    let words = text.Split ' '

    [ for count in 1 .. words.Length -> String.Join(' ', Array.take count words) ]
    |> List.tryFind runnable
    |> Option.defaultValue words[0]

let programOf (command: string) =
    match command.Trim() with
    | "" -> None
    | text when text.StartsWith Quote -> ProgramPath.tryCreate (quoted text)
    | text -> ProgramPath.tryCreate (unquoted text)
