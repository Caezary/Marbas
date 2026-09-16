module Marbas.FileSystem.ContentMatch

open System
open System.IO
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.Operations

[<Literal>]
let private BufferLength = 65536

let private comparisonFor case =
    match case with
    | IgnoringCase -> StringComparison.OrdinalIgnoreCase
    | MatchingCase -> StringComparison.Ordinal

let private isWordCharacter (character: char) =
    Char.IsLetterOrDigit character || character = '_'

let private bounded (text: string) index length atStart =
    let before =
        if index = 0 then atStart else not (isWordCharacter text[index - 1])

    let after =
        index + length = text.Length || not (isWordCharacter text[index + length])

    before && after

let rec private occursFrom (search: TextSearch) (needle: string) (text: string) atStart atEnd from =
    match text.IndexOf(needle, from, comparisonFor search.Case) with
    | -1 -> false
    | index ->
        match search.Words with
        | Anywhere -> true
        | WholeWords when not atEnd && index + needle.Length = text.Length -> false
        | WholeWords when bounded text index needle.Length atStart -> true
        | WholeWords -> occursFrom search needle text atStart atEnd (index + 1)

let private scan (control: BatchControl) (stream: Stream) (search: TextSearch) encoding =
    let needle = FindText.value search.Text
    let decoder = TextEncoding.decoder encoding
    let bytes = Array.zeroCreate BufferLength
    let chars = Array.zeroCreate (TextEncoding.maxChars encoding BufferLength)
    let tail = needle.Length + 1

    let rec step (carry: string) atStart =
        if control.IsCancelled() then
            SearchStopped
        else
            let read = stream.Read(bytes, 0, BufferLength)
            let count = decoder.GetChars(bytes, 0, read, chars, 0)
            let text = carry + String(chars, 0, count)
            let atEnd = read = 0

            if occursFrom search needle text atStart atEnd 0 then
                TextMatched
            elif atEnd then
                TextDidNotMatch
            else
                let kept =
                    if text.Length <= tail then
                        text
                    else
                        text.Substring(text.Length - tail)

                step kept (atStart && kept.Length = text.Length)

    step "" true

let private probeOf (stream: Stream) =
    let probe = Array.zeroCreate TextEncoding.ProbeLength
    let read = stream.Read(probe, 0, probe.Length)
    Array.sub probe 0 read

let private searched (fs: IFileSystem) control (search: TextSearch) (path: FileSystemPath) =
    use stream =
        fs.File.Open(FileSystemPath.value path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)

    let probe = probeOf stream
    let encoding = TextEncoding.detect probe

    match TextEncoding.contentOf probe, search.Binary with
    | Binary, Skipped -> FileSkipped
    | _ ->
        stream.Position <- int64 (TextEncoding.preambleLength encoding)
        scan control stream search encoding

let private sensed sense answer =
    match sense, answer with
    | NotContaining, TextMatched -> TextDidNotMatch
    | NotContaining, TextDidNotMatch -> TextMatched
    | _ -> answer

let search (fs: IFileSystem) control (query: TextSearch) (path: FileSystemPath) =
    try
        searched fs control query path |> sensed query.Sense
    with _ ->
        FileUnreadable
