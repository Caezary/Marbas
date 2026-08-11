namespace Marbas.FileSystem

type EntryName = private EntryName of string

module EntryName =

    let create (raw: string) = EntryName raw

    let value (EntryName raw) = raw
