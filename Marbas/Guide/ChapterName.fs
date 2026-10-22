namespace Marbas.Guide

open System

type ChapterName = private ChapterName of string

module ChapterName =

    let private extension = ".md"

    let private outsideTheDirectory = [| '/'; '\\'; ':' |]

    let tryCreate (raw: string) =
        if
            raw.Length > extension.Length
            && raw.EndsWith(extension, StringComparison.Ordinal)
            && raw.IndexOfAny outsideTheDirectory < 0
        then
            Some(ChapterName raw)
        else
            None

    let value (ChapterName name) = name

    let contents = ChapterName "README.md"
