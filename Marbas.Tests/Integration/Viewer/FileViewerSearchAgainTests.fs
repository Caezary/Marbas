module Marbas.Tests.Integration.Viewer.FileViewerSearchAgainTests

open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.Viewing
open Marbas.Ui.Viewer
open Marbas.Tests.Integration.Gui
open Marbas.Tests.Integration.Viewer.FileViewerFixture

let private backAnswer raw =
    { plainAnswer raw with
        Direction = Backwards }

[<Fact>]
let ``Shift+F7 before any search asks for one`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers None }

    use window = searching ports (numbered 10)

    again (FileViewer.content window)

    Assert.Equal(
        { Remembered = []
          Last = LastText None },
        Assert.Single offers
    )

[<Fact>]
let ``Shift+F7 finds the next match without asking`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers (Some(plainAnswer "needle")) }

    use window = searched ports (text [ "needle"; "hay"; "needle" ])
    let content = FileViewer.content window

    again content

    Assert.Single offers |> ignore
    Assert.Equal<(int * int * int) list>([ 2, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``Shift+F7 goes on past the end of the match`` () =
    use window = searched (answering "aa") (text [ "aaaa" ])
    let content = FileViewer.content window

    again content

    Assert.Equal<(int * int * int) list>([ 0, 2, 2 ], content.FoundOnScreen())

[<Fact>]
let ``Shift+F7 past the last match reports the end and keeps the mark`` () =
    let reports = ResizeArray<SearchReport>()

    let ports =
        { answering "needle" with
            Report = reports.Add }

    use window = searched ports (text [ "hay"; "needle" ])
    let content = FileViewer.content window

    again content

    Assert.Equal(TextNotFound(literally "needle"), Assert.Single reports)
    Assert.Equal<(int * int * int) list>([ 1, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``a search back starts from the bottom of the screen`` () =
    use window = searched (answeringWith (backAnswer "line 1")) (numbered 100)

    let content = FileViewer.content window

    Assert.Equal(0, content.Top)
    Assert.Equal<(int * int * int) list>([ 18, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``Shift+F7 goes on backwards after a search back`` () =
    use window = searched (answeringWith (backAnswer "line 1")) (numbered 100)
    let content = FileViewer.content window

    again content

    Assert.Equal<(int * int * int) list>([ 17, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``a search back to a match above the screen brings its line to the top`` () =
    use window = searching (answeringWith (backAnswer "line 5")) (numbered 100)
    let content = FileViewer.content window
    press content Key.PageDown

    press content Key.F7

    Assert.Equal(4, content.Top)
    Assert.Equal<(int * int * int) list>([ 0, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``a search back past the first match reports the start`` () =
    let reports = ResizeArray<SearchReport>()

    let ports =
        { answeringWith (backAnswer "needle") with
            Report = reports.Add }

    use window = searched ports (text [ "needle"; "hay" ])
    let content = FileViewer.content window

    again content

    Assert.Equal(TextNotFound(literally "needle"), Assert.Single reports)
    Assert.Equal<(int * int * int) list>([ 0, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``a search that minds the case finds nothing in the other case`` () =
    let reports = ResizeArray<SearchReport>()

    let ports =
        { answeringWith
              { plainAnswer "LINE 5" with
                  Case = MatchingCase } with
            Report = reports.Add }

    use window = searched ports (numbered 100)

    Assert.Equal(TextNotFound(literally "LINE 5"), Assert.Single reports)

[<Fact>]
let ``a search for whole words passes over part of a word`` () =
    let answer =
        { plainAnswer "needle" with
            Words = WholeWords }

    use window = searched (answeringWith answer) (text [ "needles"; "needle" ])

    let content = FileViewer.content window

    Assert.Equal<(int * int * int) list>([ 1, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``Shift+F7 repeats the options of the last search`` () =
    let answer =
        { plainAnswer "needle" with
            Words = WholeWords }

    use window =
        searched (answeringWith answer) (text [ "needle"; "needles"; "needle" ])

    let content = FileViewer.content window

    again content

    Assert.Equal<(int * int * int) list>([ 2, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``F7 pressed again offers the options of the last search`` () =
    let offers = ResizeArray<SearchOffer>()

    let answer =
        { plainAnswer "line 5" with
            Case = MatchingCase
            Words = WholeWords
            Direction = Backwards }

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers (Some answer) }

    use window = searched ports (numbered 10)

    press (FileViewer.content window) Key.F7

    Assert.Equal(
        Some
            { plainQuery "line 5" with
                Wanted = WindowSearch.Literal(wanted "line 5", WholeWords)
                Case = MatchingCase
                Direction = Backwards },
        textOffered offers[1]
    )

[<Fact>]
let ``Shift+F7 after a code page is chosen starts from the screen`` () =
    let ports =
        { answering "line 5" with
            ChooseCodePage = fun _ -> Some(Fixed Windows1250) }

    use window = searched ports (numbered 100)
    let content = FileViewer.content window
    press content Key.F8

    again content

    Assert.Equal<(int * int * int) list>([ 4, 0, 6 ], content.FoundOnScreen())

[<Fact>]
let ``Shift+F7 in hex before any search of bytes asks for one`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { answering "line 5" with
            AskSearch = recordingOffers offers (Some(plainAnswer "line 5")) }

    use window = searched ports (numbered 10)
    let content = FileViewer.content window
    press content Key.F4

    again content

    Assert.Equal(
        { Remembered = []
          Last = LastBytes None },
        offers[1]
    )
