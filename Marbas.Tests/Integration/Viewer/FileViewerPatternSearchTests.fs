module Marbas.Tests.Integration.Viewer.FileViewerPatternSearchTests

open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Viewer
open Marbas.Tests
open Marbas.Tests.Integration.Gui
open Marbas.Tests.Integration.Viewer.FileViewerFixture

let private patternAnswer raw =
    { plainAnswer raw with
        TypedAs = AsExpression }

let private patterned raw =
    WindowSearch.Pattern(SearchPattern.tryCreate raw |> Expect.ok)

let private refused report =
    match report with
    | Refused _ -> true
    | TextNotFound _
    | BytesNotFound _
    | SearchFailed _ -> false

[<Fact>]
let ``a pattern finds the first line it matches from the top of the screen`` () =
    use window = searched (answeringWith (patternAnswer "line 1[0-9]$")) (numbered 100)

    let content = FileViewer.content window

    Assert.Equal<(int * int * int) list>([ 9, 0, 7 ], content.FoundOnScreen())

[<Fact>]
let ``a pattern searched back finds the last line it matches from the bottom of the screen`` () =
    let answer =
        { patternAnswer "line 1[0-9]$" with
            Direction = Backwards }

    use window = searched (answeringWith answer) (numbered 100)

    let content = FileViewer.content window

    Assert.Equal<(int * int * int) list>([ 18, 0, 7 ], content.FoundOnScreen())

[<Fact>]
let ``Shift+F7 repeats a pattern`` () =
    use window = searched (answeringWith (patternAnswer "line 1[0-9]$")) (numbered 100)
    let content = FileViewer.content window

    again content

    Assert.Equal<(int * int * int) list>([ 10, 0, 7 ], content.FoundOnScreen())

[<Fact>]
let ``a pattern that matches nothing is reported forwards`` () =
    let reports = ResizeArray<SearchReport>()

    let ports =
        { answeringWith (patternAnswer "[xyz]+") with
            Report = reports.Add }

    use window = searched ports (numbered 100)

    Assert.Equal(TextNotFound(patterned "[xyz]+"), Assert.Single reports)

[<Fact>]
let ``a pattern that matches nothing is reported backwards`` () =
    let reports = ResizeArray<SearchReport>()

    let answer =
        { patternAnswer "[xyz]+" with
            Direction = Backwards }

    let ports =
        { answeringWith answer with
            Report = reports.Add }

    use window = searched ports (numbered 100)

    Assert.Equal(TextNotFound(patterned "[xyz]+"), Assert.Single reports)

let private brokenPatternSearched direction =
    let reports = ResizeArray<SearchReport>()
    let runs = ResizeArray<unit>()
    let remembered = ResizeArray<EnteredText>()

    let ports =
        { answeringWith
              { patternAnswer "(" with
                  Direction = direction } with
            Report = reports.Add
            RunSearch = counting runs
            Searches.Remember = remembered.Add }

    use window = searched ports (numbered 10)
    List.ofSeq reports, List.ofSeq runs, List.ofSeq remembered

[<Fact>]
let ``a broken pattern searched forwards is reported before anything is read`` () =
    let reports, runs, remembered = brokenPatternSearched Forwards

    Assert.True(refused (Assert.Single reports), $"%A{reports}")
    Assert.Empty runs
    Assert.Empty remembered

[<Fact>]
let ``a broken pattern searched backwards is reported before anything is read`` () =
    let reports, runs, remembered = brokenPatternSearched Backwards

    Assert.True(refused (Assert.Single reports), $"%A{reports}")
    Assert.Empty runs
    Assert.Empty remembered

[<Fact>]
let ``F7 pressed again offers the pattern back`` () =
    let offers = ResizeArray<SearchOffer>()

    let ports =
        { quietPorts with
            AskSearch = recordingOffers offers (Some(patternAnswer "line [0-9]+")) }

    use window = searched ports (numbered 10)

    press (FileViewer.content window) Key.F7

    Assert.Equal(
        Some
            { plainQuery "line [0-9]+" with
                Wanted = patterned "line [0-9]+" },
        textOffered offers[1]
    )
