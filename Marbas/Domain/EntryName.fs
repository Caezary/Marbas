namespace Marbas.Domain

type EntryName = private EntryName of string

module EntryName =

    let create (raw: string) = EntryName raw

    let tryCreate (raw: string) =
        match raw.Trim() with
        | "" -> None
        | trimmed -> Some(EntryName trimmed)

    let value (EntryName raw) = raw

    let extension (EntryName raw) =
        match raw.LastIndexOf '.' with
        | dot when dot <= 0 || dot = raw.Length - 1 -> ""
        | dot -> raw.Substring(dot + 1)
