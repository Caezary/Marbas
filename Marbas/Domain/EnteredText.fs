namespace Marbas.Domain

open System

type EnteredText = private EnteredText of string

module EnteredText =

    let private breaksALine (raw: string) = raw.IndexOfAny [| '\n'; '\r' |] >= 0

    let tryCreate (raw: string) =
        if String.IsNullOrWhiteSpace raw || breaksALine raw then
            None
        else
            Some(EnteredText raw)

    let value (EnteredText raw) = raw
