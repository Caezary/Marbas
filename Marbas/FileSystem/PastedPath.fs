module Marbas.FileSystem.PastedPath

open System
open System.IO.Abstractions
open System.Text.RegularExpressions
open Marbas.Domain

let private quotes = [ '"'; '\'' ]

let private breaksALine (text: string) = text.IndexOfAny [| '\n'; '\r' |] >= 0

let private unquoted (text: string) =
    match
        quotes
        |> List.tryFind (fun quote -> text.Length >= 2 && text[0] = quote && text[text.Length - 1] = quote)
    with
    | Some _ -> text.Substring(1, text.Length - 2).Trim()
    | None -> text

let private singleLine (text: string) =
    match unquoted (text.Trim()) with
    | "" -> None
    | line when breaksALine line -> None
    | line -> Some line

let private isFileUri (text: string) =
    text.StartsWith("file:", StringComparison.OrdinalIgnoreCase)

let private uriPath (text: string) =
    match Uri.TryCreate(text, UriKind.Absolute) with
    | true, uri when uri.IsFile && uri.LocalPath.Trim('\\', '/') <> "" -> Some uri.LocalPath
    | _ -> None

let private homeVariable platform =
    match platform with
    | Windows -> "USERPROFILE"
    | Unix -> "HOME"

let private isSeparator platform (character: char) =
    match platform with
    | Windows -> character = '\\' || character = '/'
    | Unix -> character = '/'

let private withHome platform (variable: string -> string option) (text: string) =
    let leadsHome =
        text = "~" || (text.Length > 1 && text[0] = '~' && isSeparator platform text[1])

    match leadsHome, variable (homeVariable platform) with
    | true, Some home -> home + text.Substring 1
    | _ -> text

let private variablePattern platform =
    match platform with
    | Windows -> Regex @"%([^%]+)%"
    | Unix -> Regex @"\$\{(\w+)\}|\$(\w+)"

let private withVariables platform (variable: string -> string option) (text: string) =
    let expanded (found: Match) =
        let name =
            if found.Groups[1].Success then
                found.Groups[1].Value
            else
                found.Groups[2].Value

        variable name |> Option.defaultValue found.Value

    (variablePattern platform).Replace(text, MatchEvaluator expanded)

let private expanded platform variable (line: string) =
    if isFileUri line then
        uriPath line
    else
        line |> withHome platform variable |> withVariables platform variable |> Some

let private holdsNoInvalidCharacter (fs: IFileSystem) (text: string) =
    text.IndexOfAny(fs.Path.GetInvalidPathChars()) < 0

let private trimmedEnd (fs: IFileSystem) (path: FileSystemPath) =
    fs.Path.TrimEndingDirectorySeparator(FileSystemPath.value path)
    |> FileSystemPath.create

let read (fs: IFileSystem) (variable: string -> string option) (directory: FileSystemPath) (text: string) =
    let platform = FileSystemPath.platformOf fs

    singleLine text
    |> Option.bind (expanded platform variable)
    |> Option.filter (holdsNoInvalidCharacter fs)
    |> Option.map (FileSystemPath.resolvedFrom fs directory >> trimmedEnd fs)
