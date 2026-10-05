module Marbas.Tests.Ui.Viewer.ViewerSearchTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Viewer
open Marbas.Tests

let private wanted raw = FindText.tryCreate raw |> Option.get

[<Fact>]
let ``the progress says how much of the file has been read`` () =
    let line = ViewerSearch.progress 1000L 250L

    Assert.Equal("Searching… 25%", line)

[<Fact>]
let ``text that is not there is named in the message`` () =
    let message = ViewerSearch.describe (TextNotFound(wanted "needle"))

    Assert.Equal("'needle' was not found.", message)

[<Fact>]
let ``a file that failed to read names the failure`` () =
    let path = FileSystemPath.create (Root.at "start/notes.txt")

    let message = ViewerSearch.describe (SearchFailed(ReadFailed(path, "the disk went away")))

    Assert.Contains("the disk went away", message)

[<Fact>]
let ``an answer becomes the query it asks for`` () =
    let answer =
        { Typed = "needle "
          Case = MatchingCase
          Words = WholeWords
          Direction = Backwards }

    let expected: WindowSearch.Query =
        { Text = wanted "needle"
          Case = MatchingCase
          Words = WholeWords
          Direction = Backwards }

    let query = ViewerSearch.query answer (wanted "needle")

    Assert.Equal(expected, query)
