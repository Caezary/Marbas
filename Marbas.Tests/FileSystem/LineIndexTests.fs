module Marbas.Tests.FileSystem.LineIndexTests

open System
open System.Text
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private notes = at "start/notes.txt"

let private over (data: MockFileData) =
    let fs = MockFileSystem()
    fs.AddFile(notes, data)
    let source = FileWindow.openFile fs (fp notes) |> Expect.ok
    source, LineIndex.Index(source, source.Encoding)

let private indexing (text: string) = over (MockFileData text) |> snd

let private textsOf (index: LineIndex.Index) count =
    [ for line in 0 .. count - 1 -> index.Text line ]

[<Fact>]
let ``a line feed ends a line`` () =
    let index = indexing "first\nsecond\nthird"

    let lines = textsOf index 3

    Assert.Equal<string option list>([ Some "first"; Some "second"; Some "third" ], lines)

[<Fact>]
let ``a carriage return before the feed is not part of the line`` () =
    let index = indexing "first\r\nsecond"

    let lines = textsOf index 2

    Assert.Equal<string option list>([ Some "first"; Some "second" ], lines)

[<Fact>]
let ``the lines are read in the encoding the index is given`` () =
    let source, _ = over (MockFileData [| 0x41uy; 0xB9uy; 0x0Auy; 0xA5uy |])

    let index =
        LineIndex.Index(
            source,
            { Charset = Windows1250
              Bom = WithoutBom }
        )

    let lines = textsOf index 2

    Assert.Equal<string option list>([ Some "Aą"; Some "Ą" ], lines)

[<Fact>]
let ``a wider encoding finds its own line breaks`` () =
    let source, _ = over (MockFileData(Encoding.Unicode.GetBytes "first\nsecond"))
    let index = LineIndex.Index(source, { Charset = Utf16Le; Bom = WithoutBom })

    let lines = textsOf index 2

    Assert.Equal<string option list>([ Some "first"; Some "second" ], lines)

[<Fact>]
let ``a feed at the very end adds no empty line`` () =
    let index = indexing "first\nsecond\n"

    index.ScanAll()

    Assert.Equal(2, index.Known)
    Assert.Equal(None, index.Text 2)

[<Fact>]
let ``an empty line between two feeds is a line of its own`` () =
    let index = indexing "first\n\nthird"

    let lines = textsOf index 3

    Assert.Equal<string option list>([ Some "first"; Some ""; Some "third" ], lines)

[<Fact>]
let ``an empty file holds no lines`` () =
    let index = indexing ""

    index.ScanAll()

    Assert.Equal(0, index.Known)
    Assert.Equal(None, index.Text 0)

[<Fact>]
let ``a line past the end is nothing`` () =
    let index = indexing "first\nsecond"

    let line = index.Text 7

    Assert.Equal(None, line)

[<Fact>]
let ``a byte order mark is no part of the first line`` () =
    let marked =
        Array.append (Encoding.UTF8.GetPreamble()) (Encoding.UTF8.GetBytes "first\nsecond")

    let index = over (MockFileData marked) |> snd

    let lines = textsOf index 2

    Assert.Equal<string option list>([ Some "first"; Some "second" ], lines)

[<Fact>]
let ``wide lines are read in their own encoding`` () =
    let wide =
        Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes "first\r\nsecond")

    let index = over (MockFileData wide) |> snd

    let lines = textsOf index 2

    Assert.Equal<string option list>([ Some "first"; Some "second" ], lines)

[<Fact>]
let ``a big endian file is read the same way`` () =
    let wide =
        Array.append (Encoding.BigEndianUnicode.GetPreamble()) (Encoding.BigEndianUnicode.GetBytes "first\nsecond")

    let index = over (MockFileData wide) |> snd

    let lines = textsOf index 2

    Assert.Equal<string option list>([ Some "first"; Some "second" ], lines)

[<Fact>]
let ``only the lines that were asked for are looked for`` () =
    let index = indexing (String.replicate 40000 "line\n")

    let first = index.Text 0

    Assert.Equal(Some "line", first)
    Assert.True(index.Known < 40000, $"{index.Known} lines were indexed to show the first one")
    Assert.False(index.Complete)

[<Fact>]
let ``asking for the whole file indexes it whole`` () =
    let index = indexing (String.replicate 40000 "line\n")

    index.ScanAll()

    Assert.Equal(40000, index.Known)
    Assert.True(index.Complete)

[<Fact>]
let ``a line spanning the end of a scanning chunk is still found`` () =
    let filler = String('x', LineIndex.ChunkLength + 40)
    let index = indexing $"{filler}\nlast"

    let lines = textsOf index 2

    Assert.Equal<string option list>([ Some filler; Some "last" ], lines)

[<Fact>]
let ``the line holding an offset is the one that started before it`` () =
    let index = indexing "first\nsecond\nthird"

    let line = index.LineAt 9L

    Assert.Equal(1, line)

[<Fact>]
let ``an offset before the first line is the first line`` () =
    let index = indexing "first\nsecond"

    let line = index.LineAt 0L

    Assert.Equal(0, line)

[<Fact>]
let ``an offset past the end is the last line`` () =
    let index = indexing "first\nsecond"

    let line = index.LineAt 4000L

    Assert.Equal(1, line)

[<Fact>]
let ``a line begins where the one before it ended`` () =
    let index = indexing "first\nsecond"

    let start = index.Start 1

    Assert.Equal(Some 6L, start)

[<Fact>]
let ``an unbroken file is one line however long it is`` () =
    let index = indexing (String('x', 5000))

    index.ScanAll()

    Assert.Equal(1, index.Known)
    Assert.Equal(Some(String('x', 5000)), index.Text 0)

let private numbered count =
    [ for line in 0 .. count - 1 -> $"line-%03d{line}\n" ] |> String.concat ""

[<Fact>]
let ``the index remembers one start for every checkpoint's worth of lines`` () =
    let index =
        indexing (String.replicate (100 * LineIndex.LinesPerCheckpoint) "line\n")

    index.ScanAll()

    Assert.Equal(100, index.Checkpoints)

[<Theory>]
[<InlineData(0, 0L)>]
[<InlineData(63, 567L)>]
[<InlineData(64, 576L)>]
[<InlineData(65, 585L)>]
[<InlineData(128, 1152L)>]
[<InlineData(199, 1791L)>]
let ``a line between two checkpoints begins where the one before it ended`` line expected =
    let index = indexing (numbered 200)

    let start = index.Start line

    Assert.Equal(Some expected, start)

[<Theory>]
[<InlineData(64)>]
[<InlineData(150)>]
[<InlineData(199)>]
let ``a line between two checkpoints reads as itself`` line =
    let index = indexing (numbered 200)

    let text = index.Text line

    Assert.Equal(Some $"line-%03d{line}", text)

[<Theory>]
[<InlineData(1353L, 150)>]
[<InlineData(1350L, 150)>]
[<InlineData(1349L, 149)>]
[<InlineData(576L, 64)>]
let ``the line holding an offset past a checkpoint is the one that started before it`` offset expected =
    let index = indexing (numbered 200)

    let line = index.LineAt offset

    Assert.Equal(expected, line)

[<Fact>]
let ``a wide line past a checkpoint reads in its own encoding`` () =
    let wide =
        Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes(numbered 200))

    let index = over (MockFileData wide) |> snd

    let text = index.Text 150

    Assert.Equal(Some "line-150", text)

[<Fact>]
let ``a whole file of many checkpoints knows every line`` () =
    let index = indexing (numbered 200)

    index.ScanAll()

    Assert.Equal(200, index.Known)

[<Fact>]
let ``the last line ends before a feed at the very end`` () =
    let index = indexing "first\nsecond\n"

    let last = index.Text 1

    Assert.Equal(Some "second", last)

[<Fact>]
let ``a wide last line ends before a feed at the very end`` () =
    let wide =
        Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes "first\r\nsecond\r\n")

    let index = over (MockFileData wide) |> snd

    let last = index.Text 1

    Assert.Equal(Some "second", last)

let private everyLine (index: LineIndex.Index) =
    index.ScanAll()
    [ for line in 0 .. index.Known - 1 -> index.Start line |> Option.get, index.Text line |> Option.get ]

let private walked (source: FileWindow.Source) from =
    LineIndex.walkFrom source source.Encoding from
    |> Seq.choose (fun step ->
        match step with
        | LineIndex.LineRead line -> Some(line.Start, line.Text)
        | LineIndex.Reading _ -> None)
    |> List.ofSeq

let private readingAt (source: FileWindow.Source) from =
    LineIndex.walkFrom source source.Encoding from
    |> Seq.choose (fun step ->
        match step with
        | LineIndex.Reading offset -> Some offset
        | LineIndex.LineRead _ -> None)
    |> List.ofSeq

let private bomUtf16 (text: string) =
    MockFileData(Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes text))

[<Theory>]
[<InlineData("first\nsecond\nthird")>]
[<InlineData("first\nsecond\n")>]
[<InlineData("first\r\nsecond\r\n")>]
[<InlineData("\n\n\nlast")>]
[<InlineData("\n")>]
[<InlineData("only")>]
[<InlineData("")>]
[<InlineData("﻿marked\nlines")>]
let ``a walk from the start passes the lines the index reads`` (content: string) =
    let source, index = over (MockFileData content)
    let expected = everyLine index

    let lines = walked source 0L

    Assert.Equal<(int64 * string) list>(expected, lines)

[<Fact>]
let ``a wide walk passes the lines the index reads`` () =
    let source, index = over (bomUtf16 "first\r\nsecond\nthird\n")
    let expected = everyLine index

    let lines = walked source 0L

    Assert.Equal<(int64 * string) list>(expected, lines)

[<Fact>]
let ``a walk passes lines that run across the window of bytes`` () =
    let source, index =
        over (MockFileData(String('a', LineIndex.ChunkLength - 3) + "\nbcdefgh\nlast"))

    let expected = everyLine index

    let lines = walked source 0L

    Assert.Equal<(int64 * string) list>(expected, lines)

[<Fact>]
let ``a walk cuts a line longer than the longest the viewer reads`` () =
    let source, index =
        over (MockFileData(String('a', LineIndex.MaxLineLength + 10) + "\nnext"))

    let expected = everyLine index

    let lines = walked source 0L

    Assert.Equal<(int64 * string) list>(expected, lines)

[<Fact>]
let ``a walk from a line start begins at that line`` () =
    let source, _ = over (MockFileData "first\nsecond\nthird")

    let lines = walked source 6L

    Assert.Equal<(int64 * string) list>([ 6L, "second"; 13L, "third" ], lines)

[<Fact>]
let ``a walk from the start of a marked file begins past the mark`` () =
    let source, _ = over (MockFileData "﻿first")

    let lines = walked source 0L

    Assert.Equal<(int64 * string) list>([ 3L, "first" ], lines)

[<Fact>]
let ``a walk says where it reads before each window of bytes`` () =
    let source, _ = over (MockFileData(String('a', LineIndex.ChunkLength * 2 + 10)))

    let offsets = readingAt source 7L

    Assert.Equal<int64 list>([ 7L; 7L + int64 LineIndex.ChunkLength; 7L + int64 LineIndex.ChunkLength * 2L ], offsets)

[<Fact>]
let ``a walk past the end reads nothing`` () =
    let source, _ = over (MockFileData "first")

    let offsets = readingAt source 5L

    Assert.Empty offsets

let private walkedBack (source: FileWindow.Source) before =
    LineIndex.walkBackFrom source source.Encoding before
    |> Seq.choose (fun step ->
        match step with
        | LineIndex.LineRead line -> Some(line.Start, line.Text)
        | LineIndex.Reading _ -> None)
    |> List.ofSeq

let private readingBackAt (source: FileWindow.Source) before =
    LineIndex.walkBackFrom source source.Encoding before
    |> Seq.choose (fun step ->
        match step with
        | LineIndex.Reading offset -> Some offset
        | LineIndex.LineRead _ -> None)
    |> List.ofSeq

let private lastStart (index: LineIndex.Index) =
    index.ScanAll()
    index.Start(index.Known - 1) |> Option.get

let private allButLastBackwards lines = lines |> List.rev |> List.tail

[<Theory>]
[<InlineData("first\nsecond\nthird")>]
[<InlineData("first\nsecond\n")>]
[<InlineData("first\r\nsecond\r\nthird")>]
[<InlineData("\n\n\nlast")>]
[<InlineData("a\n\nb")>]
[<InlineData("only\nlast")>]
[<InlineData("﻿marked\nlines\nhere")>]
let ``a walk back passes the lines before a line start the index reads, last first`` (content: string) =
    let source, index = over (MockFileData content)
    let expected = everyLine index |> allButLastBackwards

    let lines = walkedBack source (lastStart index)

    Assert.Equal<(int64 * string) list>(expected, lines)

[<Fact>]
let ``a wide walk back passes the lines the index reads`` () =
    let source, index = over (bomUtf16 "first\r\nsecond\nthird\nlast")
    let expected = everyLine index |> allButLastBackwards

    let lines = walkedBack source (lastStart index)

    Assert.Equal<(int64 * string) list>(expected, lines)

[<Fact>]
let ``a walk back passes lines that run across the window of bytes`` () =
    let content =
        "head\n"
        + String('a', LineIndex.ChunkLength + 7)
        + "\nmiddle\n"
        + String('b', 20)
        + "\nlast"

    let source, index = over (MockFileData content)
    let expected = everyLine index |> allButLastBackwards

    let lines = walkedBack source (lastStart index)

    Assert.Equal<(int64 * string) list>(expected, lines)

[<Fact>]
let ``a walk back cuts a line longer than the longest the viewer reads`` () =
    let source, index =
        over (MockFileData("first\n" + String('a', LineIndex.MaxLineLength + 10) + "\nlast"))

    let expected = everyLine index |> allButLastBackwards

    let lines = walkedBack source (lastStart index)

    Assert.Equal<(int64 * string) list>(expected, lines)

[<Fact>]
let ``a walk back from the first line passes nothing`` () =
    let source, _ = over (MockFileData "first\nsecond")

    let lines = walkedBack source 0L

    Assert.Empty lines

[<Fact>]
let ``a walk back from the first line of a marked file passes nothing`` () =
    let source, _ = over (MockFileData "﻿first\nsecond")

    let lines = walkedBack source 3L

    Assert.Empty lines

[<Fact>]
let ``a walk back says where it reads before each window of bytes`` () =
    let source, _ =
        over (MockFileData(String('a', LineIndex.ChunkLength * 2 + 10) + "\nlast"))

    let before = int64 (LineIndex.ChunkLength * 2 + 11)

    let offsets = readingBackAt source before

    Assert.Equal<int64 list>([ before - 1L - int64 LineIndex.ChunkLength; 10L; 0L ], offsets)

type private LengthCounted(bytes: byte array) =
    inherit IO.MemoryStream(bytes)

    let asked = ResizeArray<unit>()

    member _.Asked = asked.Count

    override _.Length =
        asked.Add()
        base.Length

let private countedOver lines =
    let text = String.replicate lines "line\n"
    let stream = new LengthCounted(Encoding.UTF8.GetBytes text)

    let source =
        new FileWindow.Source(fp notes, stream, { Charset = Utf8; Bom = WithoutBom })

    stream, LineIndex.Index(source, source.Encoding)

let private askedToIndex lines =
    let stream, index = countedOver lines
    index.ScanAll()
    stream.Asked

let private askedToReadTheLast lines =
    let stream, index = countedOver lines
    index.ScanAll()
    let before = stream.Asked
    index.Text(lines - 1) |> ignore
    stream.Asked - before

[<Fact>]
let ``a window of many lines asks how long the file is as often as a window of few`` () =
    let few = askedToIndex 10

    let many = askedToIndex 10000

    Assert.Equal(few, many)

[<Fact>]
let ``reading a late line asks how long the file is as often among many lines as among few`` () =
    let few = askedToReadTheLast 10

    let many = askedToReadTheLast 10000

    Assert.Equal(few, many)
