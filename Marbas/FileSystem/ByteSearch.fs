module Marbas.FileSystem.ByteSearch

open System
open Marbas.Domain
open Marbas.FileSystem.WindowSearch

type Query =
    { Bytes: WantedBytes
      Direction: SearchDirection }

type Match = { Offset: int64; Length: int }

let private firstIn (buffer: byte array) length (wanted: byte array) =
    match MemoryExtensions.IndexOf(ReadOnlySpan<byte>(buffer, 0, length), ReadOnlySpan<byte> wanted) with
    | -1 -> None
    | index -> Some index

let private lastIn (buffer: byte array) length (wanted: byte array) =
    match MemoryExtensions.LastIndexOf(ReadOnlySpan<byte>(buffer, 0, length), ReadOnlySpan<byte> wanted) with
    | -1 -> None
    | index -> Some index

let private matchAt (offset: int64) index (wanted: byte array) =
    Found
        { Offset = offset + int64 index
          Length = wanted.Length }

let rec private forwardFrom (control: Control) (source: FileWindow.Source) (buffer: byte array) wanted (offset: int64) =
    if control.IsCancelled() then
        Stopped
    else
        control.Reached offset
        let length = source.ReadInto(offset, buffer)

        match firstIn buffer length wanted with
        | Some index -> matchAt offset index wanted
        | None when length < buffer.Length -> NotFound
        | None -> forwardFrom control source buffer wanted (offset + int64 (length - wanted.Length + 1))

let rec private backFrom
    (control: Control)
    (source: FileWindow.Source)
    (buffer: byte array)
    (wanted: byte array)
    (limit: int64)
    =
    if limit < int64 wanted.Length then
        NotFound
    elif control.IsCancelled() then
        Stopped
    else
        let start = max 0L (limit - int64 buffer.Length)
        control.Reached start
        source.ReadInto(start, buffer) |> ignore

        match lastIn buffer (int (limit - start)) wanted with
        | Some index -> matchAt start index wanted
        | None when start = 0L -> NotFound
        | None -> backFrom control source buffer wanted (start + int64 (wanted.Length - 1))

let run (control: Control) (source: FileWindow.Source) (query: Query) (from: int64) : Answer<Match> =
    let wanted = WantedBytes.value query.Bytes
    let buffer = Array.zeroCreate<byte>(max LineIndex.ChunkLength (wanted.Length * 2))

    try
        match query.Direction with
        | Forwards -> forwardFrom control source buffer wanted (max 0L from)
        | Backwards -> backFrom control source buffer wanted (min source.Length (from + int64 wanted.Length - 1L))
    with ex ->
        Unreadable(ContentError.mapException source.Path ex)
