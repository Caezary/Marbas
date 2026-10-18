module Marbas.Viewing.LineIndex

open System
open System.Collections.Generic

[<Literal>]
let ChunkLength = 65536

[<Literal>]
let MaxLineLength = 1048576

[<Literal>]
let LinesPerCheckpoint = 64

[<Literal>]
let private CachedLines = 256

[<Literal>]
let private CachedBlocks = 16

let private lastAtOrBefore count (startAt: int -> int64) (offset: int64) =
    let mutable low = 0
    let mutable high = count - 1

    while low < high do
        let middle = (low + high + 1) / 2

        if startAt middle <= offset then
            low <- middle
        else
            high <- middle - 1

    low

let private withoutReturn (line: string) =
    if line.EndsWith '\r' then
        line[.. line.Length - 2]
    else
        line

type Index(source: FileWindow.Source, encoding: TextEncoding) =
    let feed = TextEncoding.lineFeed encoding
    let contentStart = int64 (TextEncoding.preambleLength encoding)
    let beforeFinalFeed = source.Length - int64 feed.Length

    let contentEnd =
        if
            beforeFinalFeed >= contentStart
            && source.Read(beforeFinalFeed, feed.Length) = feed
        then
            beforeFinalFeed
        else
            source.Length

    let chunk = Array.zeroCreate<byte> ChunkLength
    let checkpoints = ResizeArray<int64>()
    let blocks = Dictionary<int, int64 array>()
    let texts = Dictionary<int, string>()
    let mutable known = 0
    let mutable lastStart = contentStart
    let mutable scanned = contentStart
    let mutable complete = false

    let feedAt position =
        if feed.Length = 1 then
            chunk[position] = feed[0]
        else
            chunk[position] = feed[0] && chunk[position + 1] = feed[1]

    let startsInChunk (offset: int64) length (fileLength: int64) (found: int64 -> unit) =
        let mutable position = 0

        while position + feed.Length <= length do
            if feedAt position then
                let start = offset + int64 (position + feed.Length)

                if start < fileLength then
                    found start

            position <- position + feed.Length

    let note start =
        if known % LinesPerCheckpoint = 0 then
            checkpoints.Add start

        known <- known + 1
        lastStart <- start

    do
        if source.Length > contentStart then
            note contentStart
        else
            complete <- true

    let scanChunk () =
        let length = source.ReadInto(scanned, chunk)
        let fileLength = source.Length

        if length < feed.Length then
            complete <- true
        else
            startsInChunk scanned length fileLength note
            scanned <- scanned + int64 length
            complete <- scanned >= fileLength

    let scanUntilKnown line =
        while not complete && known <= line do
            scanChunk ()

    let rescanBlock block =
        let first = block * LinesPerCheckpoint
        let starts = Array.zeroCreate (min LinesPerCheckpoint (known - first))
        let count = ref 1
        let mutable offset = checkpoints[block]
        let mutable length = 1
        starts[0] <- offset

        let found start =
            if count.Value < starts.Length then
                starts[count.Value] <- start
                count.Value <- count.Value + 1

        while count.Value < starts.Length && length > 0 do
            length <- source.ReadInto(offset, chunk)
            startsInChunk offset length source.Length found
            offset <- offset + int64 length

        starts

    let blockStarts block =
        match blocks.TryGetValue block with
        | true, starts -> starts
        | _ ->
            let starts = rescanBlock block

            if starts.Length = LinesPerCheckpoint || complete then
                if blocks.Count >= CachedBlocks then
                    blocks.Clear()

                blocks[block] <- starts

            starts

    member _.Known = known

    member _.Checkpoints = checkpoints.Count

    member _.Complete = complete

    member _.ScanAll() =
        while not complete do
            scanChunk ()

    member _.Start(line: int) =
        scanUntilKnown line

        if line >= 0 && line < known then
            let starts = blockStarts (line / LinesPerCheckpoint)
            Some starts[line % LinesPerCheckpoint]
        else
            None

    member this.Text(line: int) : string option =
        match texts.TryGetValue line with
        | true, text -> Some text
        | _ ->
            match this.Start line with
            | None -> None
            | Some start ->
                let stop =
                    match this.Start(line + 1) with
                    | Some next -> next - int64 feed.Length
                    | None -> contentEnd

                let length = int (min (stop - start) (int64 MaxLineLength))

                let text =
                    source.Read(start, length) |> TextEncoding.decode encoding |> withoutReturn

                if texts.Count >= CachedLines then
                    texts.Clear()

                texts[line] <- text
                Some text

    member _.LineAt(offset: int64) =
        while not complete && lastStart < offset do
            scanChunk ()

        if known = 0 then
            0
        else
            let block = lastAtOrBefore checkpoints.Count (fun at -> checkpoints[at]) offset
            let starts = blockStarts block

            block * LinesPerCheckpoint
            + lastAtOrBefore starts.Length (fun at -> starts[at]) offset

type Line = { Start: int64; Text: string }

type Step =
    | Reading of offset: int64
    | LineRead of Line

type private Unfinished =
    { From: int64; Held: ResizeArray<byte> }

let private beginningAt offset = { From = offset; Held = ResizeArray() }

let private finished encoding (unfinished: Unfinished) =
    { Start = unfinished.From
      Text = unfinished.Held.ToArray() |> TextEncoding.decode encoding |> withoutReturn }

let private hold (unfinished: Unfinished) (chunk: byte array) first stop =
    let count = min (MaxLineLength - unfinished.Held.Count) (stop - first)

    if count > 0 then
        unfinished.Held.AddRange(ArraySegment(chunk, first, count))

let private isFeedAt (feed: byte array) (chunk: byte array) position =
    if feed.Length = 1 then
        chunk[position] = feed[0]
    else
        chunk[position] = feed[0] && chunk[position + 1] = feed[1]

let private split encoding (feed: byte array) (chunk: byte array) length (offset: int64) unfinished =
    let feedAt = isFeedAt feed chunk
    let lines = ResizeArray<Line>()
    let mutable current = unfinished
    let mutable lineFrom = 0
    let mutable position = 0

    while position + feed.Length <= length do
        if feedAt position then
            hold current chunk lineFrom position
            lines.Add(finished encoding current)
            lineFrom <- position + feed.Length
            current <- beginningAt (offset + int64 lineFrom)

        position <- position + feed.Length

    hold current chunk lineFrom length
    List.ofSeq lines, current

let walkFrom (source: FileWindow.Source) encoding (from: int64) =
    let feed = TextEncoding.lineFeed encoding
    let first = max from (int64 (TextEncoding.preambleLength encoding))
    let chunk = Array.zeroCreate<byte> ChunkLength

    let rec walk (offset: int64) unfinished =
        seq {
            if offset >= source.Length then
                if unfinished.From < source.Length then
                    yield LineRead(finished encoding unfinished)
            else
                yield Reading offset
                let length = source.ReadInto(offset, chunk)
                let lines, rest = split encoding feed chunk length offset unfinished
                yield! List.map LineRead lines
                yield! walk (offset + int64 length) rest
        }

    walk first (beginningAt first)

let private lineBetween (source: FileWindow.Source) encoding (chunk: byte array) (chunkStart: int64) start stop =
    let count = int (min (stop - start) (int64 MaxLineLength))

    let within = start - chunkStart
    let inChunk = within >= 0L && within + int64 count <= int64 chunk.Length

    let bytes =
        if inChunk then
            Array.sub chunk (int within) count
        else
            source.Read(start, count)

    { Start = start
      Text = bytes |> TextEncoding.decode encoding |> withoutReturn }

let private splitBack source encoding (feed: byte array) chunk (chunkStart: int64) (scanned: int64) lineEnd =
    let feedAt = isFeedAt feed chunk
    let lines = ResizeArray<Line>()
    let mutable stop = lineEnd
    let mutable position = int (scanned - chunkStart) - feed.Length

    while position >= 0 do
        if feedAt position then
            let start = chunkStart + int64 (position + feed.Length)
            lines.Add(lineBetween source encoding chunk chunkStart start stop)
            stop <- chunkStart + int64 position

        position <- position - feed.Length

    List.ofSeq lines, stop

let walkBackFrom (source: FileWindow.Source) encoding (before: int64) =
    let feed = TextEncoding.lineFeed encoding
    let contentStart = int64 (TextEncoding.preambleLength encoding)
    let chunk = Array.zeroCreate<byte> ChunkLength

    let rec walk (scanned: int64) lineEnd =
        seq {
            if scanned <= contentStart then
                yield LineRead(lineBetween source encoding chunk contentStart contentStart lineEnd)
            else
                let chunkStart = max contentStart (scanned - int64 ChunkLength)
                yield Reading chunkStart
                source.ReadInto(chunkStart, chunk) |> ignore
                let lines, rest = splitBack source encoding feed chunk chunkStart scanned lineEnd
                yield! List.map LineRead lines
                yield! walk chunkStart rest
        }

    let lastEnd = before - int64 feed.Length

    if before <= contentStart then
        Seq.empty
    else
        walk lastEnd lastEnd
