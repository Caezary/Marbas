module Marbas.Tests.Integration.Viewer.FileViewerSearchTests

open System
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Ui.Viewer
open Marbas.Tests.Integration.Gui
open Marbas.Tests.Integration.Viewer.FileViewerFixture

let private fp = FileSystemPath.create

let private stopping (work: WindowSearch.Control -> WindowSearch.Answer<WindowSearch.Match>) =
    work
        { Reached = ignore
          IsCancelled = fun () -> true }

[<Fact>]
let ``F7 offers the texts searched before and nothing typed yet`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers None
            Searches.Texts = fun () -> [ typed "older" ] }

    use window = searched ports (numbered 10)

    Assert.Equal(
        { Remembered = [ typed "older" ]
          Last = LastText None },
        Assert.Single offers
    )

[<Fact>]
let ``F7 pressed again offers the last text searched for`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers (Some(plainAnswer "line 5")) }

    use window = searched ports (numbered 10)

    press (FileViewer.content window) Key.F7

    Assert.Equal(LastText(Some(plainQuery "line 5")), offers[1].Last)

[<Fact>]
let ``a text searched for is remembered as it was typed`` () =
    let remembered = ResizeArray<EnteredText>()

    let ports =
        { answering "line 5 " with
            Searches.Remember = remembered.Add }

    use window = searched ports (numbered 10)

    Assert.Equal(typed "line 5 ", Assert.Single remembered)

[<Fact>]
let ``an empty field searches for nothing`` () =
    let runs = ResizeArray<unit>()

    let ports =
        { answering "" with
            RunSearch = counting runs }

    use window = searched ports (numbered 10)

    Assert.Empty runs

[<Fact>]
let ``a popup closed without a text searches for nothing`` () =
    let runs = ResizeArray<unit>()

    let ports =
        { quietPorts with
            RunSearch = counting runs }

    use window = searched ports (numbered 10)

    Assert.Empty runs

[<Fact>]
let ``a match on the screen leaves the screen where it stands`` () =
    use window = searched (answering "line 5") (numbered 100)

    let content = FileViewer.content window

    Assert.Equal(0, content.Top)
    Assert.Equal<(int * int * int) list>([ 4, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``a match further down a file larger than one window of bytes comes to the top`` () =
    use window = searched (answering "line 9000") (numbered 10000)

    let content = FileViewer.content window

    Assert.Equal(8999, content.Top)
    Assert.Equal("line 9000", List.head (content.Rows()))
    Assert.Equal<(int * int * int) list>([ 0, 0, 9 ], content.FoundOnScreen())

[<Fact>]
let ``a search starts from the top of the screen`` () =
    use window =
        searching (answering "needle") (text [ "needle"; "hay"; "hay"; "needle" ])

    let content = FileViewer.content window
    press content Key.CursorDown

    press content Key.F7

    Assert.Equal<(int * int * int) list>([ 2, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``text that is not in the file is reported and the screen stays`` () =
    let reports = ResizeArray<SearchReport>()

    let ports =
        { answering "absent" with
            Report = reports.Add }

    use window = searching ports (numbered 100)
    let content = FileViewer.content window
    press content Key.CursorDown

    press content Key.F7

    Assert.Equal(TextNotFound(literally "absent"), Assert.Single reports)
    Assert.Equal(1, content.Top)
    Assert.Empty(content.FoundOnScreen())

[<Fact>]
let ``a search stopped with Esc leaves the screen and says nothing`` () =
    let reports = ResizeArray<SearchReport>()

    let ports =
        { answering "line 90" with
            RunSearch = stopping
            Report = reports.Add }

    use window = searched ports (numbered 100)

    let content = FileViewer.content window
    Assert.Empty reports
    Assert.Equal(0, content.Top)
    Assert.Empty(content.FoundOnScreen())

[<Fact>]
let ``a file that fails while it is searched is reported`` () =
    let reports = ResizeArray<SearchReport>()
    let failure = ReadFailed(fp notes, "the disk went away")

    let ports =
        { answering "line 90" with
            RunSearch = fun _ -> WindowSearch.Unreadable failure
            Report = reports.Add }

    use window = searched ports (numbered 100)

    Assert.Equal(SearchFailed failure, Assert.Single reports)

[<Fact>]
let ``a match past the right edge moves the screen across to it`` () =
    use window = searched (answering "needle") (text [ String('a', 100) + "needle" ])

    let content = FileViewer.content window

    Assert.Equal(26, content.Left)
    Assert.Equal<(int * int * int) list>([ 0, 74, 6 ], content.FoundOnScreen())

[<Fact>]
let ``a match in a wrapped line is marked on the row it falls on`` () =
    use window = searching (answering "needle") (text [ String('a', 100) + "needle" ])
    let content = FileViewer.content window
    press content Key.F2

    press content Key.F7

    Assert.Equal<(int * int * int) list>([ 1, 20, 6 ], content.FoundOnScreen())

[<Fact>]
let ``a match the wrap splits is marked on both rows`` () =
    use window = searching (answering "needle") (text [ String('a', 77) + "needle" ])
    let content = FileViewer.content window
    press content Key.F2

    press content Key.F7

    Assert.Equal<(int * int * int) list>([ 0, 77, 3; 1, 0, 3 ], content.FoundOnScreen())

[<Fact>]
let ``a match after a tab is marked where it is shown`` () =
    use window = searched (answering "needle") (text [ "a\tneedle" ])

    let content = FileViewer.content window

    Assert.Equal<(int * int * int) list>([ 0, 8, 6 ], content.FoundOnScreen())

[<Fact>]
let ``the search reads the file in the code page chosen`` () =
    let ports =
        { answering "Ą" with
            ChooseCodePage = fun _ -> Some(Fixed Windows1250) }

    use window = searching ports latinBytes
    let content = FileViewer.content window
    press content Key.F8

    press content Key.F7

    Assert.Equal<(int * int * int) list>([ 0, 1, 1 ], content.FoundOnScreen())

[<Fact>]
let ``choosing another code page drops the mark`` () =
    let ports =
        { answering "line 5" with
            ChooseCodePage = fun _ -> Some(Fixed Windows1250) }

    use window = searched ports (numbered 100)
    let content = FileViewer.content window

    press content Key.F8

    Assert.Empty(content.FoundOnScreen())

[<Fact>]
let ``F7 in hex offers the bytes searched before and nothing typed yet`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers None
            Searches.Texts = fun () -> [ typed "text" ]
            ByteSearches.Texts = fun () -> [ typed "4D 5A" ] }

    use window = searching ports (numbered 10)
    let content = FileViewer.content window
    press content Key.F4

    press content Key.F7

    Assert.Equal(
        { Remembered = [ typed "4D 5A" ]
          Last = LastBytes None },
        Assert.Single offers
    )

[<Fact>]
let ``the key bar offers the search`` () =
    use window = laidOut (text [ "first" ])

    let labels =
        FunctionKeyBar.slots (FileViewer.keyBar window) |> List.map _.CommandName.Text

    Assert.Equal("Search", labels[6])
