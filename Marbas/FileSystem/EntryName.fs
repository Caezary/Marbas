namespace Marbas.FileSystem

type EntryName = private EntryName of string

module EntryName =

    let create (raw: string) = EntryName raw

    let tryCreate (raw: string) =
        match raw.Trim() with
        | "" -> None
        | trimmed -> Some(EntryName trimmed)

    let value (EntryName raw) = raw
