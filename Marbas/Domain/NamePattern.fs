namespace Marbas.Domain

open System

type NamePattern = private NamePattern of string

module NamePattern =

    type private Token =
        | AnyRun
        | AnyOne
        | Literal of char

    type private Backtrack = { AfterRun: int; From: int }

    let private charAt (text: string) index =
        if index < text.Length then Some text[index] else None

    let rec private tokensFrom pattern index =
        match charAt pattern index, charAt pattern (index + 1) with
        | None, _ -> []
        | Some '\\', Some escaped -> Literal escaped :: tokensFrom pattern (index + 2)
        | Some '*', _ -> AnyRun :: tokensFrom pattern (index + 1)
        | Some '?', _ -> AnyOne :: tokensFrom pattern (index + 1)
        | Some character, _ -> Literal character :: tokensFrom pattern (index + 1)

    let private sameLetter (left: char) (right: char) =
        Char.ToUpperInvariant left = Char.ToUpperInvariant right

    let rec private matchesFrom (tokens: Token array) name token index backtrack =
        let retry () =
            match backtrack with
            | Some run ->
                let later = { run with From = run.From + 1 }
                matchesFrom tokens name later.AfterRun later.From (Some later)
            | None -> false

        match Array.tryItem token tokens, charAt name index with
        | None, None -> true
        | Some AnyRun, _ -> matchesFrom tokens name (token + 1) index (Some { AfterRun = token + 1; From = index })
        | Some AnyOne, Some _ -> matchesFrom tokens name (token + 1) (index + 1) backtrack
        | Some(Literal expected), Some actual when sameLetter expected actual ->
            matchesFrom tokens name (token + 1) (index + 1) backtrack
        | _, None -> false
        | _ -> retry ()

    let tryCreate (raw: string) =
        if String.IsNullOrEmpty raw then
            None
        else
            Some(NamePattern raw)

    let value (NamePattern pattern) = pattern

    let matches (NamePattern pattern) name =
        let tokens = tokensFrom pattern 0 |> Array.ofList
        matchesFrom tokens (EntryName.value name) 0 0 None
