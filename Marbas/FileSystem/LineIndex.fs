module Marbas.FileSystem.LineIndex

open System.Collections.Generic

[<Literal>]
let ChunkLength = 65536

[<Literal>]
let MaxLineLength = 1048576

[<Literal>]
let private CachedLines = 256

type Index(source: FileWindow.Source) =
    let encoding = source.Encoding
    let feed = TextEncoding.lineFeed encoding
    let contentStart = int64 (TextEncoding.preambleLength encoding)
    let starts = ResizeArray<int64>()
    let texts = Dictionary<int, string>()
    let mutable scanned = contentStart
    let mutable complete = false

    do
        if source.Length > contentStart then
            starts.Add contentStart
        else
            complete <- true

    let scanChunk () =
        let chunk = source.Read(scanned, ChunkLength)

        if chunk.Length < feed.Length then
            complete <- true
        else
            let mutable position = 0

            while position + feed.Length <= chunk.Length do
                let found =
                    if feed.Length = 1 then
                        chunk[position] = feed[0]
                    else
                        chunk[position] = feed[0] && chunk[position + 1] = feed[1]

                if found then
                    let start = scanned + int64 (position + feed.Length)

                    if start < source.Length then
                        starts.Add start

                position <- position + feed.Length

            scanned <- scanned + int64 chunk.Length
            complete <- scanned >= source.Length

    let scanUntilKnown line =
        while not complete && starts.Count <= line do
            scanChunk ()

    let withoutReturn (line: string) =
        if line.EndsWith '\r' then line[.. line.Length - 2] else line

    member _.Known = starts.Count

    member _.Complete = complete

    member _.ScanAll() =
        while not complete do
            scanChunk ()

    member _.Start(line: int) =
        scanUntilKnown line

        if line < starts.Count then Some starts[line] else None

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
                    | None -> source.Length

                let length = int (min (stop - start) (int64 MaxLineLength))

                let text =
                    source.Read(start, length) |> TextEncoding.decode encoding |> withoutReturn

                if texts.Count >= CachedLines then
                    texts.Clear()

                texts[line] <- text
                Some text

    member _.LineAt(offset: int64) =
        while not complete && starts[starts.Count - 1] < offset do
            scanChunk ()

        if starts.Count = 0 then
            0
        else
            let mutable low = 0
            let mutable high = starts.Count - 1

            while low < high do
                let middle = (low + high + 1) / 2

                if starts[middle] <= offset then low <- middle else high <- middle - 1

            low
