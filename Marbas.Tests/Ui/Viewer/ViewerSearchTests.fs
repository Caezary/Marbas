module Marbas.Tests.Ui.Viewer.ViewerSearchTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Viewer
open Marbas.Tests

let private wanted raw = FindText.tryCreate raw |> Expect.some

let private pattern raw =
    SearchPattern.tryCreate raw |> Expect.ok

let private bytes raw = WantedBytes.tryParse raw |> Expect.ok

let private typed raw : SearchAnswer =
    { Typed = raw
      Case = IgnoringCase
      Words = Anywhere
      TypedAs = AsText
      Direction = Forwards }

[<Fact>]
let ``the progress says how much of the file has been read`` () =
    let line = ViewerSearch.progress 1000L 250L

    Assert.Equal("Searching… 25%", line)

[<Fact>]
let ``text that is not there is named in the message`` () =
    let message =
        ViewerSearch.describe (TextNotFound(WindowSearch.Literal(wanted "needle", Anywhere)))

    Assert.Equal("'needle' was not found.", message)

[<Fact>]
let ``a pattern that matched nothing is named in the message`` () =
    let message =
        ViewerSearch.describe (TextNotFound(WindowSearch.Pattern(pattern "[0-9]+")))

    Assert.Equal("'[0-9]+' was not found.", message)

[<Fact>]
let ``bytes that are not there are named in the message`` () =
    let message = ViewerSearch.describe (BytesNotFound(bytes "4d5a"))

    Assert.Equal("'4D 5A' was not found.", message)

[<Fact>]
let ``a file that failed to read names the failure`` () =
    let path = FileSystemPath.create (Root.at "start/notes.txt")

    let message =
        ViewerSearch.describe (SearchFailed(ReadFailed(path, "the disk went away")))

    Assert.Contains("the disk went away", message)

[<Fact>]
let ``a refused pattern names what the parser said`` () =
    let message =
        ViewerSearch.describe (Refused(BrokenPattern(PatternDoesNotParse "Not enough )'s.")))

    Assert.Contains("Not enough )'s.", message)

[<Fact>]
let ``a token that is not a byte is named in the message`` () =
    let message = ViewerSearch.describe (Refused(BrokenBytes(NotAByte "4G")))

    Assert.Contains("'4G'", message)

[<Fact>]
let ``a field with no bytes says what it wants`` () =
    let message = ViewerSearch.describe (Refused(BrokenBytes NoBytes))

    Assert.Contains("hexadecimal", message)

[<Fact>]
let ``an answer becomes the query it asks for`` () =
    let answer =
        { typed "needle " with
            Case = MatchingCase
            Words = WholeWords
            Direction = Backwards }

    let expected: WindowSearch.Query =
        { Wanted = WindowSearch.Literal(wanted "needle ", WholeWords)
          Case = MatchingCase
          Direction = Backwards }

    let query = ViewerSearch.query answer

    Assert.Equal(Some(Ok(TextAsked expected)), query)

[<Fact>]
let ``an answer typed as an expression becomes a pattern`` () =
    let answer =
        { typed "[0-9]+" with
            TypedAs = AsExpression }

    let expected: WindowSearch.Query =
        { Wanted = WindowSearch.Pattern(pattern "[0-9]+")
          Case = IgnoringCase
          Direction = Forwards }

    let query = ViewerSearch.query answer

    Assert.Equal(Some(Ok(TextAsked expected)), query)

[<Fact>]
let ``an expression is not narrowed to whole words`` () =
    let answer =
        { typed "[0-9]+" with
            TypedAs = AsExpression
            Words = WholeWords }

    let expected: WindowSearch.Query =
        { Wanted = WindowSearch.Pattern(pattern "[0-9]+")
          Case = IgnoringCase
          Direction = Forwards }

    let query = ViewerSearch.query answer

    Assert.Equal(Some(Ok(TextAsked expected)), query)

[<Fact>]
let ``an expression that does not parse is refused`` () =
    let answer =
        { typed "(" with
            TypedAs = AsExpression }

    let query = ViewerSearch.query answer

    Assert.True(query |> Option.exists Result.isError, $"%A{query}")

[<Fact>]
let ``an answer typed as bytes becomes a search for them`` () =
    let answer =
        { typed "4D 5A" with
            TypedAs = AsBytes
            Case = MatchingCase
            Words = WholeWords
            Direction = Backwards }

    let expected: ByteSearch.Query =
        { Bytes = bytes "4D 5A"
          Direction = Backwards }

    let query = ViewerSearch.query answer

    Assert.Equal(Some(Ok(BytesAsked expected)), query)

[<Fact>]
let ``an answer typed as bytes that are not bytes is refused`` () =
    let answer = { typed "4D 5G" with TypedAs = AsBytes }

    let query = ViewerSearch.query answer

    Assert.Equal(Some(Error(BrokenBytes(NotAByte "5G"))), query)

[<Fact>]
let ``an empty answer asks for nothing`` () =
    let answer = { typed "" with TypedAs = AsExpression }

    let query = ViewerSearch.query answer

    Assert.Equal(None, query)
