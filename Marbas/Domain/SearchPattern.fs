namespace Marbas.Domain

open System
open System.Text.RegularExpressions

type SearchPattern = private SearchPattern of string

type PatternError =
    | PatternDoesNotParse of reason: string
    | PatternNotSupported of reason: string

module PatternError =

    let reason error =
        match error with
        | PatternDoesNotParse reason
        | PatternNotSupported reason -> reason

module SearchPattern =

    let private linear = RegexOptions.NonBacktracking ||| RegexOptions.CultureInvariant

    let private options case =
        match case with
        | IgnoringCase -> linear ||| RegexOptions.IgnoreCase
        | MatchingCase -> linear

    let tryCreate (raw: string) =
        try
            Regex(raw, options MatchingCase) |> ignore
            Ok(SearchPattern raw)
        with
        | :? NotSupportedException as refused -> Error(PatternNotSupported refused.Message)
        | :? ArgumentException as broken -> Error(PatternDoesNotParse broken.Message)

    let value (SearchPattern raw) = raw

    let regex case (SearchPattern raw) = Regex(raw, options case)
