module Marbas.FileSystem.NamePattern

open System.IO.Enumeration

let matches (pattern: string) (name: string) =
    not (System.String.IsNullOrEmpty pattern)
    && FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase = true)
