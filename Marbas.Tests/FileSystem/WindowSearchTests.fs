module Marbas.Tests.FileSystem.WindowSearchTests

open System
open System.IO
open System.Text
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.WindowSearch
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private notes = at "start/notes.txt"

let private sourceOf (data: MockFileData) =
    let fs = MockFileSystem()
    fs.AddFile(notes, data)
    FileWindow.openFile fs (fp notes) |> Expect.ok

let private wanted raw = FindText.tryCreate raw |> Expect.some

let private going: Control =
    { Reached = ignore
      IsCancelled = fun () -> false }

let private plain raw : Query =
    { Wanted = Literal(wanted raw, Anywhere)
      Case = IgnoringCase
      Direction = Forwards }

let private expression raw : Query =
    { Wanted = Pattern(SearchPattern.tryCreate raw |> Expect.ok)
      Case = IgnoringCase
      Direction = Forwards }

let private placeAt lineStart column : Place =
    { LineStart = lineStart
      Column = column }

let private searchingFor query (content: string) lineStart column =
    use source = sourceOf (MockFileData content)
    run going source source.Encoding query (placeAt lineStart column)

let private searching content raw from = searchingFor (plain raw) content from 0

let private matching raw = { plain raw with Case = MatchingCase }

let private whole raw =
    { plain raw with
        Wanted = Literal(wanted raw, WholeWords) }

let private back (query: Query) = { query with Direction = Backwards }

let private found lineStart column length =
    Found
        { LineStart = lineStart
          Column = column
          Length = length }

type private BrokenStream() =
    inherit MemoryStream(Array.zeroCreate<byte> 100)

    override _.Read(_: byte array, _: int, _: int) : int =
        raise (IOException "the disk went away")

    override _.Read(_: Span<byte>) : int =
        raise (IOException "the disk went away")

[<Fact>]
let ``text on the line it starts from is found there`` () =
    let answer = searching "alpha beta\ngamma" "beta" 0L

    Assert.Equal(
        Found
            { LineStart = 0L
              Column = 6
              Length = 4 },
        answer
    )

[<Fact>]
let ``text further down is found on its own line`` () =
    let answer = searching "alpha\nbeta gamma\ndelta" "gamma" 0L

    Assert.Equal(
        Found
            { LineStart = 6L
              Column = 5
              Length = 5 },
        answer
    )

[<Fact>]
let ``a plain search does not mind the letter case`` () =
    let answer = searching "alpha\nGamma" "gAMMA" 0L

    Assert.Equal(
        Found
            { LineStart = 6L
              Column = 0
              Length = 5 },
        answer
    )

[<Fact>]
let ``the first of several matches is the one found`` () =
    let answer = searching "one\ntwo two\ntwo" "two" 0L

    Assert.Equal(
        Found
            { LineStart = 4L
              Column = 0
              Length = 3 },
        answer
    )

[<Fact>]
let ``what lies before the line the search starts from is not found`` () =
    let answer = searching "needle\nhay\nneedle" "needle" 4L

    Assert.Equal(
        Found
            { LineStart = 11L
              Column = 0
              Length = 6 },
        answer
    )

[<Fact>]
let ``text past the first window of bytes is found`` () =
    let filler = String.replicate 5000 "hay and more hay\n"
    let content = filler + "the needle\n"

    let answer = searching content "needle" 0L

    Assert.Equal(
        Found
            { LineStart = int64 filler.Length
              Column = 4
              Length = 6 },
        answer
    )

[<Fact>]
let ``text that is not in the file is not found`` () =
    let answer = searching "alpha\nbeta" "gamma" 0L

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``text is not matched across a line break`` () =
    let answer = searching "alpha\nbeta" "a b" 0L

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``an empty file holds nothing`` () =
    let answer = searching "" "gamma" 0L

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``text is matched in the encoding it is read in`` () =
    use source = sourceOf (MockFileData [| 0x41uy; 0x0Auy; 0x42uy; 0xA5uy |])

    let encoding =
        { Charset = Windows1250
          Bom = WithoutBom }

    let answer = run going source encoding (plain "Ą") (placeAt 0L 0)

    Assert.Equal(
        Found
            { LineStart = 2L
              Column = 1
              Length = 1 },
        answer
    )

[<Fact>]
let ``a wide file is searched by its characters`` () =
    let wide =
        Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes "first\nthe needle")

    use source = sourceOf (MockFileData wide)

    let answer = run going source source.Encoding (plain "needle") (placeAt 0L 0)

    Assert.Equal(
        Found
            { LineStart = 14L
              Column = 4
              Length = 6 },
        answer
    )

[<Fact>]
let ``a search told to stop stops before reading`` () =
    use source = sourceOf (MockFileData "the needle")

    let stopping =
        { going with
            IsCancelled = fun () -> true }

    let answer = run stopping source source.Encoding (plain "needle") (placeAt 0L 0)

    Assert.Equal(Stopped, answer)

[<Fact>]
let ``a search says how far it has read`` () =
    use source = sourceOf (MockFileData(String('a', LineIndex.ChunkLength + 10)))
    let reached = ResizeArray<int64>()

    let watching = { going with Reached = reached.Add }

    run watching source source.Encoding (plain "needle") (placeAt 0L 0) |> ignore

    Assert.Equal<int64 list>([ 0L; int64 LineIndex.ChunkLength ], List.ofSeq reached)

[<Fact>]
let ``a file that fails to read is answered with the failure`` () =
    use source =
        new FileWindow.Source(fp notes, new BrokenStream(), { Charset = Utf8; Bom = WithoutBom })

    let answer = run going source source.Encoding (plain "needle") (placeAt 0L 0)

    Assert.Equal(Unreadable(ReadFailed(fp notes, "the disk went away")), answer)

[<Fact>]
let ``a search from a column passes over what lies before it on the line`` () =
    let answer = searchingFor (plain "needle") "needle and needle" 0L 1

    Assert.Equal(found 0L 11 6, answer)

[<Fact>]
let ``a match at the very end of the file is found`` () =
    let answer = searching "hay\nhay needle" "needle" 0L

    Assert.Equal(found 4L 4 6, answer)

[<Fact>]
let ``a search past the last match ends at the end of the file`` () =
    let answer = searchingFor (plain "needle") "hay\nneedle" 4L 1

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``a search that minds the case passes over the other case`` () =
    let answer = searchingFor (matching "Needle") "needle\nNeedle" 0L 0

    Assert.Equal(found 7L 0 6, answer)

[<Fact>]
let ``a search that minds the case finds nothing in the other case`` () =
    let answer = searchingFor (matching "NEEDLE") "needle" 0L 0

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``a search for whole words passes over part of a word`` () =
    let answer = searchingFor (whole "need") "needle\nneed it" 0L 0

    Assert.Equal(found 7L 0 4, answer)

[<Fact>]
let ``a whole word may fill the line`` () =
    let answer = searchingFor (whole "need") "need" 0L 0

    Assert.Equal(found 0L 0 4, answer)

[<Fact>]
let ``digits and underscores belong to a word`` () =
    let answer = searchingFor (whole "need") "need_1 need2 need." 0L 0

    Assert.Equal(found 0L 13 4, answer)

[<Fact>]
let ``punctuation ends a word`` () =
    let answer = searchingFor (whole "need") "(need)" 0L 0

    Assert.Equal(found 0L 1 4, answer)

[<Fact>]
let ``a search back finds the text in a line above`` () =
    let answer = searchingFor (back (plain "needle")) "needle\nhay\nneedle" 11L 0

    Assert.Equal(found 0L 0 6, answer)

[<Fact>]
let ``a search back looks before the column on the line it starts from`` () =
    let answer = searchingFor (back (plain "needle")) "needle needle" 0L 7

    Assert.Equal(found 0L 0 6, answer)

[<Fact>]
let ``a search back from the end of a line finds its last match`` () =
    let answer = searchingFor (back (plain "a")) "a a a" 0L 5

    Assert.Equal(found 0L 4 1, answer)

[<Fact>]
let ``a search back finds the last match of a line above`` () =
    let answer = searchingFor (back (plain "a")) "a a a\nhay" 6L 0

    Assert.Equal(found 0L 4 1, answer)

[<Fact>]
let ``a search back ends at the start of the file`` () =
    let answer = searchingFor (back (plain "needle")) "hay\nneedle" 4L 0

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``a search back finds text before the window of bytes it starts in`` () =
    let filler = String.replicate 5000 "hay and more hay\n"
    let content = "the needle\n" + filler + "last"

    let answer =
        searchingFor (back (plain "needle")) content (int64 (11 + filler.Length)) 0

    Assert.Equal(found 0L 4 6, answer)

[<Fact>]
let ``a wide file is searched back by its characters`` () =
    let wide =
        Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes "the needle\nlast")

    use source = sourceOf (MockFileData wide)

    let answer =
        run going source source.Encoding (back (plain "needle")) (placeAt 24L 0)

    Assert.Equal(found 2L 4 6, answer)

[<Fact>]
let ``a search back told to stop stops before reading`` () =
    use source = sourceOf (MockFileData "needle\nlast")

    let stopping =
        { going with
            IsCancelled = fun () -> true }

    let answer =
        run stopping source source.Encoding (back (plain "needle")) (placeAt 7L 0)

    Assert.Equal(Stopped, answer)

[<Fact>]
let ``a search back says how far it has read`` () =
    use source =
        sourceOf (MockFileData(String('a', LineIndex.ChunkLength + 10) + "\nlast"))

    let start = int64 (LineIndex.ChunkLength + 11)
    let reached = ResizeArray<int64>()

    let watching = { going with Reached = reached.Add }

    run watching source source.Encoding (back (plain "needle")) (placeAt start 0)
    |> ignore

    Assert.Equal<int64 list>([ start; 10L; 0L ], List.ofSeq reached)

[<Fact>]
let ``case and whole words narrow a search together`` () =
    let query =
        { whole "Need" with
            Case = MatchingCase }

    let answer = searchingFor query "need Needle Need" 0L 0

    Assert.Equal(found 0L 12 4, answer)

[<Fact>]
let ``case narrows a search back`` () =
    let answer = searchingFor (back (matching "Need")) "Need need\nneed" 10L 0

    Assert.Equal(found 0L 0 4, answer)

[<Fact>]
let ``whole words narrow a search back`` () =
    let answer = searchingFor (back (whole "need")) "need needle\nneedles" 12L 0

    Assert.Equal(found 0L 0 4, answer)

[<Fact>]
let ``every option at once searches forward`` () =
    let query =
        { whole "Need" with
            Case = MatchingCase }

    let answer = searchingFor query "need Need Needle need\nx" 0L 0

    Assert.Equal(found 0L 5 4, answer)

[<Fact>]
let ``every option at once searches back`` () =
    let query =
        back
            { whole "Need" with
                Case = MatchingCase }

    let answer = searchingFor query "need Need Needle need\nx" 22L 0

    Assert.Equal(found 0L 5 4, answer)

[<Fact>]
let ``every option at once ends at the start of the file`` () =
    let query =
        back
            { whole "Need" with
                Case = MatchingCase }

    let answer = searchingFor query "need Need" 0L 5

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``every option at once ends at the end of the file`` () =
    let query =
        { whole "Need" with
            Case = MatchingCase }

    let answer = searchingFor query "Need Needle" 0L 4

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``a pattern finds the first text it matches`` () =
    let answer = searchingFor (expression "[0-9]+") "alpha\nbeta 42 gamma" 0L 0

    Assert.Equal(found 6L 5 2, answer)

[<Fact>]
let ``a match is as long as what the pattern took`` () =
    let answer = searchingFor (expression "a+") "x aaa" 0L 0

    Assert.Equal(found 0L 2 3, answer)

[<Fact>]
let ``a pattern does not mind the case unless told`` () =
    let answer = searchingFor (expression "ne+dle") "the NEEDLE" 0L 0

    Assert.Equal(found 0L 4 6, answer)

[<Fact>]
let ``a pattern that minds the case finds nothing in the other`` () =
    let query =
        { expression "ne+dle" with
            Case = MatchingCase }

    let answer = searchingFor query "the NEEDLE" 0L 0

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``a pattern is matched against one line at a time`` () =
    let answer = searchingFor (expression "^line") "a line\nline 2" 0L 0

    Assert.Equal(found 7L 0 4, answer)

[<Fact>]
let ``the end of a pattern is the end of the line`` () =
    let answer = searchingFor (expression "end$") "the end of it\nthe end" 0L 0

    Assert.Equal(found 14L 4 3, answer)

[<Fact>]
let ``a pattern never runs on past the line`` () =
    let answer = searchingFor (expression "a.*") "abc\ndef" 0L 0

    Assert.Equal(found 0L 0 3, answer)

[<Fact>]
let ``a match of nothing is passed over`` () =
    let answer = searchingFor (expression "x*") "abc\nxx" 0L 0

    Assert.Equal(found 4L 0 2, answer)

[<Fact>]
let ``a pattern that only matches nothing finds nothing`` () =
    let answer = searchingFor (expression "^$") "abc\n\nxyz" 0L 0

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``a pattern that matches nothing in the file finds nothing`` () =
    let answer = searchingFor (expression "[0-9]") "alpha\nbeta" 0L 0

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``a pattern searched from a column passes over what lies before it`` () =
    let answer = searchingFor (expression "[a-z][0-9]") "a1 b2" 0L 1

    Assert.Equal(found 0L 3 2, answer)

[<Fact>]
let ``a pattern searched back finds the last match of a line above`` () =
    let answer = searchingFor (back (expression "[0-9]+")) "42 and 7\nnone" 9L 0

    Assert.Equal(found 0L 7 1, answer)

[<Fact>]
let ``a pattern searched back looks before the column`` () =
    let answer = searchingFor (back (expression "[a-z][0-9]")) "a1 b2 c3" 0L 6

    Assert.Equal(found 0L 3 2, answer)

[<Fact>]
let ``a pattern searched back finds text before the window it starts in`` () =
    let filler = String.replicate 5000 "hay and more hay\n"
    let content = "id 1234\n" + filler + "last"

    let answer =
        searchingFor (back (expression "[0-9]{4}")) content (int64 (8 + filler.Length)) 0

    Assert.Equal(found 0L 3 4, answer)

[<Fact>]
let ``a pattern searched back that matches nothing ends at the start of the file`` () =
    let answer = searchingFor (back (expression "[0-9]")) "alpha\nbeta" 6L 4

    Assert.Equal(NotFound, answer)

[<Fact>]
let ``a pattern is matched in the code page the file is read in`` () =
    use source = sourceOf (MockFileData [| 0x41uy; 0x0Auy; 0x42uy; 0xA5uy |])

    let encoding =
        { Charset = Windows1250
          Bom = WithoutBom }

    let answer = run going source encoding (expression "B[ĄĘ]") (placeAt 0L 0)

    Assert.Equal(found 2L 0 2, answer)
