module Marbas.Tests.Integration.Help.HelpViewSearchTests

open System.Drawing
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.Viewing
open Marbas.Ui.Keys
open Marbas.Ui.Viewer
open Marbas.Ui.Help
open Marbas.Tests
open Marbas.Tests.Integration.Gui
open Marbas.Tests.Integration.Help.HelpViewFixture

let private typed raw =
    EnteredText.tryCreate raw |> Expect.some

let private plainAnswer raw : SearchAnswer =
    { Typed = raw
      Case = IgnoringCase
      Words = Anywhere
      TypedAs = AsText
      Direction = Forwards }

let private plainQuery raw : WindowSearch.Query =
    { Wanted = WindowSearch.Literal(FindText.tryCreate raw |> Expect.some, Anywhere)
      Case = IgnoringCase
      Direction = Forwards }

let private answering answer =
    { quietPorts with
        AskSearch = fun _ -> Some answer }

let private recordingOffers (offers: ResizeArray<SearchOffer>) answer offer =
    offers.Add offer
    answer

let private searched ports =
    let window = showing ports (Size(80, 24))
    press (HelpView.content window) Key.F7
    window

let private foundText (content: HelpView.ContentView) =
    let row, start, length = content.FoundOnScreen() |> Expect.some
    (content.Rows()[row]).Substring(start, length)

[<Fact>]
let ``the key bar says F7 searches`` () =
    use window = laidOut ()

    let labels =
        FunctionKeyBar.slots (HelpView.keyBar window) |> List.map _.CommandName.Text

    Assert.Equal("Search", labels[6])

[<Fact>]
let ``F7 first offers nothing typed yet`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers None }

    use window = searched ports

    Assert.Equal(
        { Remembered = []
          Last = LastText None },
        Assert.Single offers
    )

[<Fact>]
let ``F7 pressed again offers the last text searched for`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers (Some(plainAnswer "chapter")) }

    use window = searched ports

    press (HelpView.content window) Key.F7

    Assert.Equal(LastText(Some(plainQuery "chapter")), offers[1].Last)

[<Fact>]
let ``F7 pressed again offers the texts searched for since the help opened`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers (Some(plainAnswer "chapter ")) }

    use window = searched ports

    press (HelpView.content window) Key.F7

    Assert.Equal<EnteredText list>([ typed "chapter " ], offers[1].Remembered)

[<Fact>]
let ``F7 finds the text and shows it on screen`` () =
    use window = searched (answering (plainAnswer "CHAPTER"))

    let found = foundText (HelpView.content window)

    Assert.Equal("chapter", found)

[<Fact>]
let ``a text the chapter does not hold is reported as not found`` () =
    let reported = ResizeArray<SearchReport>()

    let ports =
        { answering (plainAnswer "haystack") with
            ReportSearch = reported.Add }

    use window = searched ports

    Assert.Equal((plainQuery "haystack").Wanted |> TextNotFound, Assert.Single reported)

[<Fact>]
let ``an expression that does not parse is reported and searches for nothing`` () =
    let reported = ResizeArray<SearchReport>()

    let ports =
        { answering
              { plainAnswer "(" with
                  TypedAs = AsExpression } with
            ReportSearch = reported.Add }

    use window = searched ports

    Assert.Equal(Refused(BrokenPattern(SearchPattern.tryCreate "(" |> Expect.error)), Assert.Single reported)
    Assert.Equal(None, (HelpView.content window).FoundOnScreen())

[<Fact>]
let ``a popup closed without a text searches for nothing`` () =
    use window = searched quietPorts

    let found = (HelpView.content window).FoundOnScreen()

    Assert.Equal(None, found)

[<Fact>]
let ``Shift+F7 before any search asks for one`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers None }

    use window = showing ports (Size(80, 24))

    press (HelpView.content window) Key.F7.WithShift

    Assert.Single offers |> ignore

[<Fact>]
let ``Shift+F7 finds the next match without the popup`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers (Some(plainAnswer "key")) }

    use window = searched ports
    let content = HelpView.content window
    let first = content.State.Page.Found

    press content Key.F7.WithShift

    Assert.Single offers |> ignore
    Assert.NotEqual(first, content.State.Page.Found)
    Assert.Equal("key", foundText content)
