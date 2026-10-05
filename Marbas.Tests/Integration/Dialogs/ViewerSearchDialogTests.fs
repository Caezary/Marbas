module Marbas.Tests.Integration.Dialogs.ViewerSearchDialogTests

open System.Drawing
open Xunit
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Ui.Viewer

let private typed raw = EnteredText.tryCreate raw |> Option.get

let private wanted raw = FindText.tryCreate raw |> Option.get

let private openedOn (offer: SearchOffer) =
    DialogStyle.apply ()
    let dialog = ViewerSearchDialog.create offer
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private fresh () =
    openedOn { Remembered = []; Last = None }

let private listed (dialog: Dialog) =
    let field = ViewerSearchDialog.field dialog :?> DropDownList
    [ for item in field.Source.ToList() -> string item ]

let private AcceptingButton = 1

[<Fact>]
let ``the field offers the texts searched before, newest first`` () =
    use dialog =
        openedOn
            { Remembered = [ typed "newer"; typed "older" ]
              Last = None }

    let offered = listed dialog

    Assert.Equal<string list>([ "newer"; "older" ], offered)

[<Fact>]
let ``a first search starts from an empty field`` () =
    use dialog = fresh ()

    let text = (ViewerSearchDialog.field dialog).Text

    Assert.Equal("", text)

let private lastQuery raw : WindowSearch.Query =
    { Text = wanted raw
      Case = IgnoringCase
      Words = Anywhere
      Direction = Forwards }

let private ticked (dialog: Dialog) =
    ViewerSearchDialog.boxes
    |> List.filter (fun box -> (ViewerSearchDialog.checkBox dialog box).Value = CheckState.Checked)

let private tick (dialog: Dialog) box =
    (ViewerSearchDialog.checkBox dialog box).Value <- CheckState.Checked

[<Fact>]
let ``a search asked again holds the last text`` () =
    use dialog =
        openedOn
            { Remembered = [ typed "older" ]
              Last = Some(lastQuery "needle") }

    let text = (ViewerSearchDialog.field dialog).Text

    Assert.Equal("needle", text)

[<Fact>]
let ``a search asked again holds the last options`` () =
    let last =
        { lastQuery "needle" with
            Case = MatchingCase
            Words = WholeWords
            Direction = Backwards }

    use dialog = openedOn { Remembered = []; Last = Some last }

    let checkedBoxes = ticked dialog

    Assert.Equal<ViewerSearchDialog.SearchBox list>(
        [ ViewerSearchDialog.CaseBox; ViewerSearchDialog.WordsBox; ViewerSearchDialog.BackwardsBox ],
        checkedBoxes
    )

[<Fact>]
let ``a first search starts with no option ticked`` () =
    use dialog = fresh ()

    let checkedBoxes = ticked dialog

    Assert.Empty checkedBoxes

[<Fact>]
let ``the four options are there`` () =
    use dialog = fresh ()

    let labels =
        ViewerSearchDialog.boxes
        |> List.map (fun box -> (ViewerSearchDialog.checkBox dialog box).Text)

    Assert.Equal<string list>([ "C_ase sensitive"; "_Whole words"; "_Regular expression"; "_Backwards" ], labels)

[<Fact>]
let ``case, whole words and the direction can be chosen`` () =
    use dialog = fresh ()

    let enabled =
        ViewerSearchDialog.boxes
        |> List.filter (fun box -> (ViewerSearchDialog.checkBox dialog box).Enabled)

    Assert.Equal<ViewerSearchDialog.SearchBox list>(
        [ ViewerSearchDialog.CaseBox; ViewerSearchDialog.WordsBox; ViewerSearchDialog.BackwardsBox ],
        enabled
    )

[<Fact>]
let ``everything stays inside the frame`` () =
    use dialog = fresh ()

    let lowest =
        ViewerSearchDialog.boxes
        |> List.map (fun box -> (ViewerSearchDialog.checkBox dialog box).Frame)
        |> List.maxBy _.Bottom

    Assert.True(lowest.Bottom <= dialog.Viewport.Height, $"the box at %A{lowest} is clipped by %A{dialog.Viewport}")
    Assert.True(lowest.Right <= dialog.Viewport.Width, $"the box at %A{lowest} is clipped by %A{dialog.Viewport}")

[<Fact>]
let ``a confirmed popup yields what was typed, forward and plain`` () =
    use dialog = fresh ()
    (ViewerSearchDialog.field dialog).Text <- "needle "
    dialog.Result <- AcceptingButton

    let answer = ViewerSearchDialog.answer dialog

    Assert.Equal(
        Some
            { Typed = "needle "
              Case = IgnoringCase
              Words = Anywhere
              Direction = Forwards },
        answer
    )

[<Fact>]
let ``a confirmed popup yields the options ticked`` () =
    use dialog = fresh ()
    (ViewerSearchDialog.field dialog).Text <- "needle"
    tick dialog ViewerSearchDialog.CaseBox
    tick dialog ViewerSearchDialog.WordsBox
    tick dialog ViewerSearchDialog.BackwardsBox
    dialog.Result <- AcceptingButton

    let answer = ViewerSearchDialog.answer dialog

    Assert.Equal(
        Some
            { Typed = "needle"
              Case = MatchingCase
              Words = WholeWords
              Direction = Backwards },
        answer
    )

[<Fact>]
let ``a popup closed without confirming yields nothing`` () =
    use dialog = fresh ()
    (ViewerSearchDialog.field dialog).Text <- "needle"

    let answer = ViewerSearchDialog.answer dialog

    Assert.Equal(None, answer)
