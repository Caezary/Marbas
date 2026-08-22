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
    source, LineIndex.Index source

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
    let marked = Array.append (Encoding.UTF8.GetPreamble()) (Encoding.UTF8.GetBytes "first\nsecond")
    let index = over (MockFileData marked) |> snd

    let lines = textsOf index 2

    Assert.Equal<string option list>([ Some "first"; Some "second" ], lines)

[<Fact>]
let ``wide lines are read in their own encoding`` () =
    let wide = Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes "first\r\nsecond")
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
