module Marbas.FileSystem.LineIndex

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

        if startAt middle <= offset then low <- middle else high <- middle - 1

    low

type Index(source: FileWindow.Source) =
    let encoding = source.Encoding
    let feed = TextEncoding.lineFeed encoding
    let contentStart = int64 (TextEncoding.preambleLength encoding)
    let beforeFinalFeed = source.Length - int64 feed.Length

    let contentEnd =
        if beforeFinalFeed >= contentStart && source.Read(beforeFinalFeed, feed.Length) = feed then
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

    let startsInChunk (offset: int64) length (found: int64 -> unit) =
        let mutable position = 0

        while position + feed.Length <= length do
            if feedAt position then
                let start = offset + int64 (position + feed.Length)

                if start < source.Length then
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

        if length < feed.Length then
            complete <- true
        else
            startsInChunk scanned length note
            scanned <- scanned + int64 length
            complete <- scanned >= source.Length

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
            startsInChunk offset length found
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

    let withoutReturn (line: string) =
        if line.EndsWith '\r' then line[.. line.Length - 2] else line

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
            block * LinesPerCheckpoint + lastAtOrBefore starts.Length (fun at -> starts[at]) offset
