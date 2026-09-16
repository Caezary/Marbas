module Marbas.Tests.FileSystem.ContentMatchTests

open System
open System.Text
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private notes = at "start/notes.txt"

let private missing = at "start/gone.txt"

let private withBytes (bytes: byte array) =
    let fs = MockFileSystem()
    fs.AddFile(notes, MockFileData bytes)
    fs

let private withText (text: string) = withBytes (Encoding.UTF8.GetBytes text)

let private searchFor raw =
    { Text = FindText.tryCreate raw |> Expect.some
      Case = IgnoringCase
      Words = Anywhere
      Sense = Containing
      Binary = Skipped }

let private stopped: Operations.BatchControl =
    { Report = ignore
      IsCancelled = fun () -> true }

let private answerFor fs search =
    ContentMatch.search fs Operations.unattended search (fp notes)

[<Theory>]
[<InlineData("let x = 1", "let")>]
[<InlineData("let x = 1", "LET")>]
[<InlineData("LET x = 1", "let")>]
[<InlineData("zażółć gęślą jaźń", "gęślą")>]
[<InlineData("one\ntwo\nthree", "two")>]
let ``text that is in the file is found`` content needle =
    let answer = answerFor (withText content) (searchFor needle)

    Assert.Equal<ContentAnswer>(TextMatched, answer)

[<Theory>]
[<InlineData("let x = 1", "mutable")>]
[<InlineData("", "let")>]
[<InlineData("one\ntwo", "two\nthree")>]
let ``text that is not in the file is not found`` content needle =
    let answer = answerFor (withText content) (searchFor needle)

    Assert.Equal<ContentAnswer>(TextDidNotMatch, answer)

[<Fact>]
let ``a case-sensitive search tells the cases apart`` () =
    let search = { searchFor "let" with Case = MatchingCase }

    let answer = answerFor (withText "LET x = 1") search

    Assert.Equal<ContentAnswer>(TextDidNotMatch, answer)

[<Theory>]
[<InlineData("a let x")>]
[<InlineData("let x")>]
[<InlineData("x let")>]
[<InlineData("(let)")>]
[<InlineData("let.")>]
let ``a whole-word search matches a word standing on its own`` content =
    let search = { searchFor "let" with Words = WholeWords }

    let answer = answerFor (withText content) search

    Assert.Equal<ContentAnswer>(TextMatched, answer)

[<Theory>]
[<InlineData("letter")>]
[<InlineData("outlet")>]
[<InlineData("_let")>]
[<InlineData("let2")>]
[<InlineData("2let")>]
let ``a whole-word search passes over a word inside another`` content =
    let search = { searchFor "let" with Words = WholeWords }

    let answer = answerFor (withText content) search

    Assert.Equal<ContentAnswer>(TextDidNotMatch, answer)

[<Fact>]
let ``a file with a UTF-8 byte order mark is searched as text`` () =
    let bytes = Array.append (Encoding.UTF8.GetPreamble()) (Encoding.UTF8.GetBytes "let x = 1")

    let answer = answerFor (withBytes bytes) (searchFor "let")

    Assert.Equal<ContentAnswer>(TextMatched, answer)

[<Fact>]
let ``a UTF-16 little-endian file is searched in its own encoding`` () =
    let bytes = Array.append (Encoding.Unicode.GetPreamble()) (Encoding.Unicode.GetBytes "let x = 1")

    let answer = answerFor (withBytes bytes) (searchFor "let")

    Assert.Equal<ContentAnswer>(TextMatched, answer)

[<Fact>]
let ``a UTF-16 big-endian file is searched in its own encoding`` () =
    let bytes =
        Array.append (Encoding.BigEndianUnicode.GetPreamble()) (Encoding.BigEndianUnicode.GetBytes "let x = 1")

    let answer = answerFor (withBytes bytes) (searchFor "let")

    Assert.Equal<ContentAnswer>(TextMatched, answer)

[<Theory>]
[<InlineData(65530)>]
[<InlineData(65533)>]
[<InlineData(65535)>]
[<InlineData(65536)>]
[<InlineData(65537)>]
[<InlineData(131070)>]
let ``a match straddling a buffer boundary is found`` padding =
    let content = String('a', padding) + "needle" + String('b', 20)

    let answer = answerFor (withText content) (searchFor "needle")

    Assert.Equal<ContentAnswer>(TextMatched, answer)

[<Theory>]
[<InlineData(65530)>]
[<InlineData(65533)>]
[<InlineData(65535)>]
[<InlineData(65536)>]
[<InlineData(65537)>]
let ``a whole word straddling a buffer boundary keeps its boundaries`` padding =
    let content = String('a', padding) + " let " + String('b', 20)
    let search = { searchFor "let" with Words = WholeWords }

    let answer = answerFor (withText content) search

    Assert.Equal<ContentAnswer>(TextMatched, answer)

[<Theory>]
[<InlineData(65533)>]
[<InlineData(65535)>]
[<InlineData(65536)>]
let ``a word straddling a buffer boundary inside another word is passed over`` padding =
    let content = String('a', padding) + " letter " + String('b', 20)
    let search = { searchFor "let" with Words = WholeWords }

    let answer = answerFor (withText content) search

    Assert.Equal<ContentAnswer>(TextDidNotMatch, answer)

[<Fact>]
let ``a binary file is passed over unless it is asked for`` () =
    let bytes = Array.append (Encoding.UTF8.GetBytes "let x = 1") [| 0uy; 1uy; 2uy |]

    let answer = answerFor (withBytes bytes) (searchFor "let")

    Assert.Equal<ContentAnswer>(FileSkipped, answer)

[<Fact>]
let ``a binary file asked for is searched as Latin-1`` () =
    let bytes = Array.append (Encoding.UTF8.GetBytes "let x = 1") [| 0uy; 1uy; 2uy |]
    let search = { searchFor "let" with Binary = Searched }

    let answer = answerFor (withBytes bytes) search

    Assert.Equal<ContentAnswer>(TextMatched, answer)

[<Fact>]
let ``a file that cannot be read is neither a match nor a miss`` () =
    let fs = withText "let x = 1"

    let answer = ContentMatch.search fs Operations.unattended (searchFor "let") (fp missing)

    Assert.Equal<ContentAnswer>(FileUnreadable, answer)

[<Fact>]
let ``a file without the text matches a search for what does not contain it`` () =
    let search = { searchFor "let" with Sense = NotContaining }

    let answer = answerFor (withText "mutable x = 1") search

    Assert.Equal<ContentAnswer>(TextMatched, answer)

[<Fact>]
let ``a file with the text fails a search for what does not contain it`` () =
    let search = { searchFor "let" with Sense = NotContaining }

    let answer = answerFor (withText "let x = 1") search

    Assert.Equal<ContentAnswer>(TextDidNotMatch, answer)

[<Fact>]
let ``a file that cannot be read never passes as one that lacks the text`` () =
    let fs = withText "let x = 1"
    let search = { searchFor "let" with Sense = NotContaining }

    let answer = ContentMatch.search fs Operations.unattended search (fp missing)

    Assert.Equal<ContentAnswer>(FileUnreadable, answer)

[<Fact>]
let ``a stopped search answers that it was stopped`` () =
    let fs = withText "let x = 1"

    let answer = ContentMatch.search fs stopped (searchFor "let") (fp notes)

    Assert.Equal<ContentAnswer>(SearchStopped, answer)
