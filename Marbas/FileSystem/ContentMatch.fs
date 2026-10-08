module Marbas.FileSystem.ContentMatch

open System
open System.IO
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.Operations

[<Literal>]
let private BufferLength = 65536

let private bounded (text: string) index length atStart =
    let before =
        if index = 0 then
            atStart
        else
            not (WordMatch.isWordCharacter text[index - 1])

    let after =
        index + length = text.Length
        || not (WordMatch.isWordCharacter text[index + length])

    before && after

let private runsIntoUnreadText (text: string) index length atEnd =
    not atEnd && index + length = text.Length

let rec private occursFrom (search: TextSearch) (needle: string) (text: string) atStart atEnd from =
    match text.IndexOf(needle, from, LetterCase.comparison search.Case) with
    | -1 -> false
    | index ->
        match search.Words with
        | Anywhere -> true
        | WholeWords when runsIntoUnreadText text index needle.Length atEnd -> false
        | WholeWords when bounded text index needle.Length atStart -> true
        | WholeWords -> occursFrom search needle text atStart atEnd (index + 1)

let private scan (control: BatchControl) (stream: Stream) (search: TextSearch) encoding (leading: byte array) =
    let needle = FindText.value search.Text
    let decoder = TextEncoding.decoder encoding
    let bytes = Array.zeroCreate BufferLength
    let chars = Array.zeroCreate (TextEncoding.maxChars encoding BufferLength)
    let tail = needle.Length + 1

    let rec step (carry: string) atStart (held: byte array option) =
        if control.IsCancelled() then
            SearchStopped
        else
            let buffer, read =
                match held with
                | Some already -> already, already.Length
                | None -> bytes, stream.Read(bytes, 0, BufferLength)

            let count = decoder.GetChars(buffer, 0, read, chars, 0)
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

                step kept (atStart && kept.Length = text.Length) None

    step "" true (Some leading)

let private probeOf (stream: Stream) =
    let probe = Array.zeroCreate TextEncoding.ProbeLength
    let read = stream.Read(probe, 0, probe.Length)
    Array.sub probe 0 read

let private beyondPreamble (probe: byte array) encoding =
    let preamble = min (TextEncoding.preambleLength encoding) probe.Length
    Array.sub probe preamble (probe.Length - preamble)

let private answered control (search: TextSearch) (stream: Stream) =
    let probe = probeOf stream
    let encoding = TextEncoding.detect probe

    match TextEncoding.contentOf probe, search.Binary with
    | Binary, Skipped -> FileSkipped
    | _ -> scan control stream search encoding (beyondPreamble probe encoding)

let private searched (fs: IFileSystem) control (search: TextSearch) (path: FileSystemPath) =
    use stream =
        fs.File.Open(FileSystemPath.value path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)

    answered control search stream

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

let searchStream control (query: TextSearch) (stream: Stream) =
    try
        answered control query stream |> sensed query.Sense
    with _ ->
        FileUnreadable
