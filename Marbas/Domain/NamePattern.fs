namespace Marbas.Domain

open System
open System.IO.Enumeration

type NamePattern = private NamePattern of string

module NamePattern =

    let tryCreate (raw: string) =
        if String.IsNullOrEmpty raw then None else Some(NamePattern raw)

    let matches (NamePattern pattern) name =
        FileSystemName.MatchesSimpleExpression(pattern, EntryName.value name, ignoreCase = true)
