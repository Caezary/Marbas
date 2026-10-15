module Marbas.Tests.Integration.Dialogs.ViewerSearchDialogTests

open System.Drawing
open Xunit
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Ui.Viewer
open Marbas.Tests

let private typed raw =
    EnteredText.tryCreate raw |> Expect.some

let private wanted raw = FindText.tryCreate raw |> Expect.some

let private openedRefusingInto (refusals: ResizeArray<SearchRefusal>) (offer: SearchOffer) =
    DialogStyle.apply ()
    let dialog = ViewerSearchDialog.create refusals.Add offer
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private openedOn offer =
    openedRefusingInto (ResizeArray()) offer

let private fresh () =
    openedOn
        { Remembered = []
          Last = LastText None }

let private listed (dialog: Dialog) =
    let field = ViewerSearchDialog.field dialog :?> DropDownList
    [ for item in field.Source.ToList() -> string item ]

let private AcceptingButton = 1

let private lastQuery raw : WindowSearch.Query =
    { Wanted = WindowSearch.Literal(wanted raw, Anywhere)
      Case = IgnoringCase
      Direction = Forwards }

let private lastPattern raw : WindowSearch.Query =
    { Wanted = WindowSearch.Pattern(SearchPattern.tryCreate raw |> Expect.ok)
      Case = IgnoringCase
      Direction = Forwards }

let private ticked (dialog: Dialog) =
    ViewerSearchDialog.boxes
    |> List.filter (fun box -> (ViewerSearchDialog.checkBox dialog box).Value = CheckState.Checked)

let private enabled (dialog: Dialog) =
    ViewerSearchDialog.boxes
    |> List.filter (fun box -> (ViewerSearchDialog.checkBox dialog box).Enabled)

let private tick (dialog: Dialog) box =
    (ViewerSearchDialog.checkBox dialog box).Value <- CheckState.Checked

let private untick (dialog: Dialog) box =
    (ViewerSearchDialog.checkBox dialog box).Value <- CheckState.UnChecked

let private confirm (dialog: Dialog) =
    (Seq.last dialog.Buttons).InvokeCommand Command.Accept |> ignore

let private plainAnswer raw : SearchAnswer =
    { Typed = raw
      Case = IgnoringCase
      Words = Anywhere
      TypedAs = AsText
      Direction = Forwards }

[<Fact>]
let ``the field offers the texts searched before, newest first`` () =
    use dialog =
        openedOn
            { Remembered = [ typed "newer"; typed "older" ]
              Last = LastText None }

    let offered = listed dialog

    Assert.Equal<string list>([ "newer"; "older" ], offered)

[<Fact>]
let ``a first search starts from an empty field`` () =
    use dialog = fresh ()

    let text = (ViewerSearchDialog.field dialog).Text

    Assert.Equal("", text)

[<Fact>]
let ``a search asked again holds the last text`` () =
    use dialog =
        openedOn
            { Remembered = [ typed "older" ]
              Last = LastText(Some(lastQuery "needle")) }

    let text = (ViewerSearchDialog.field dialog).Text

    Assert.Equal("needle", text)

[<Fact>]
let ``a search asked again holds the last options`` () =
    let last =
        { lastQuery "needle" with
            Wanted = WindowSearch.Literal(wanted "needle", WholeWords)
            Case = MatchingCase
            Direction = Backwards }

    use dialog =
        openedOn
            { Remembered = []
              Last = LastText(Some last) }

    let checkedBoxes = ticked dialog

    Assert.Equal<ViewerSearchDialog.SearchBox list>(
        [ ViewerSearchDialog.CaseBox
          ViewerSearchDialog.WordsBox
          ViewerSearchDialog.BackwardsBox ],
        checkedBoxes
    )

[<Fact>]
let ``a pattern asked again holds its text and its box`` () =
    use dialog =
        openedOn
            { Remembered = []
              Last = LastText(Some(lastPattern "[0-9]+")) }

    let text = (ViewerSearchDialog.field dialog).Text
    let checkedBoxes = ticked dialog

    Assert.Equal("[0-9]+", text)
    Assert.Equal<ViewerSearchDialog.SearchBox list>([ ViewerSearchDialog.ExpressionBox ], checkedBoxes)

[<Fact>]
let ``a pattern asked again greys out whole words`` () =
    use dialog =
        openedOn
            { Remembered = []
              Last = LastText(Some(lastPattern "[0-9]+")) }

    let choosable = enabled dialog

    Assert.DoesNotContain(ViewerSearchDialog.WordsBox, choosable)

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
let ``every option can be chosen`` () =
    use dialog = fresh ()

    let choosable = enabled dialog

    Assert.Equal<ViewerSearchDialog.SearchBox list>(ViewerSearchDialog.boxes, choosable)

[<Fact>]
let ``ticking the regular expression greys out whole words`` () =
    use dialog = fresh ()

    tick dialog ViewerSearchDialog.ExpressionBox

    Assert.DoesNotContain(ViewerSearchDialog.WordsBox, enabled dialog)

[<Fact>]
let ``unticking the regular expression gives whole words back`` () =
    use dialog = fresh ()
    tick dialog ViewerSearchDialog.ExpressionBox

    untick dialog ViewerSearchDialog.ExpressionBox

    Assert.Contains(ViewerSearchDialog.WordsBox, enabled dialog)

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

    Assert.Equal(Some(plainAnswer "needle "), answer)

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
            { plainAnswer "needle" with
                Case = MatchingCase
                Words = WholeWords
                Direction = Backwards },
        answer
    )

[<Fact>]
let ``a confirmed popup yields an expression without whole words`` () =
    use dialog = fresh ()
    (ViewerSearchDialog.field dialog).Text <- "[0-9]+"
    tick dialog ViewerSearchDialog.WordsBox
    tick dialog ViewerSearchDialog.ExpressionBox
    dialog.Result <- AcceptingButton

    let answer = ViewerSearchDialog.answer dialog

    Assert.Equal(
        Some
            { plainAnswer "[0-9]+" with
                TypedAs = AsExpression },
        answer
    )

[<Fact>]
let ``a popup closed without confirming yields nothing`` () =
    use dialog = fresh ()
    (ViewerSearchDialog.field dialog).Text <- "needle"

    let answer = ViewerSearchDialog.answer dialog

    Assert.Equal(None, answer)

[<Fact>]
let ``confirming a pattern that does not parse refuses it and stays open`` () =
    let refusals = ResizeArray<SearchRefusal>()

    use dialog =
        openedRefusingInto
            refusals
            { Remembered = []
              Last = LastText None }

    (ViewerSearchDialog.field dialog).Text <- "("
    tick dialog ViewerSearchDialog.ExpressionBox

    confirm dialog

    Assert.Single refusals |> ignore
    Assert.False(dialog.Result.HasValue, $"the popup closed with {dialog.Result}")

[<Fact>]
let ``Enter in the field on a pattern the linear engine cannot run refuses it`` () =
    let refusals = ResizeArray<SearchRefusal>()

    use dialog =
        openedRefusingInto
            refusals
            { Remembered = []
              Last = LastText None }

    (ViewerSearchDialog.field dialog).Text <- "a(?=b)"
    tick dialog ViewerSearchDialog.ExpressionBox

    (ViewerSearchDialog.field dialog).InvokeCommand Command.Accept |> ignore

    Assert.Single refusals |> ignore
    Assert.False(dialog.Result.HasValue, $"the popup closed with {dialog.Result}")

[<Fact>]
let ``Enter on the box just ticked refuses a pattern that does not parse`` () =
    let refusals = ResizeArray<SearchRefusal>()

    use dialog =
        openedRefusingInto
            refusals
            { Remembered = []
              Last = LastText None }

    (ViewerSearchDialog.field dialog).Text <- "["
    tick dialog ViewerSearchDialog.ExpressionBox

    (ViewerSearchDialog.checkBox dialog ViewerSearchDialog.ExpressionBox).InvokeCommand Command.Accept
    |> ignore

    Assert.Single refusals |> ignore
    Assert.False(dialog.Result.HasValue, $"the popup closed with {dialog.Result}")

[<Fact>]
let ``a refused pattern hands the focus back to the field`` () =
    use dialog =
        openedOn
            { Remembered = []
              Last = LastText None }

    (ViewerSearchDialog.field dialog).Text <- "("
    let expression = ViewerSearchDialog.checkBox dialog ViewerSearchDialog.ExpressionBox
    tick dialog ViewerSearchDialog.ExpressionBox
    expression.SetFocus() |> ignore

    expression.InvokeCommand Command.Accept |> ignore

    Assert.True((ViewerSearchDialog.field dialog).HasFocus, "the field should hold the focus")

[<Fact>]
let ``confirming a pattern that parses refuses nothing`` () =
    let refusals = ResizeArray<SearchRefusal>()

    use dialog =
        openedRefusingInto
            refusals
            { Remembered = []
              Last = LastText None }

    (ViewerSearchDialog.field dialog).Text <- "[0-9]+"
    tick dialog ViewerSearchDialog.ExpressionBox

    confirm dialog

    Assert.Empty refusals

[<Fact>]
let ``confirming plain text that would not parse refuses nothing`` () =
    let refusals = ResizeArray<SearchRefusal>()

    use dialog =
        openedRefusingInto
            refusals
            { Remembered = []
              Last = LastText None }

    (ViewerSearchDialog.field dialog).Text <- "("

    confirm dialog

    Assert.Empty refusals

let private freshBytes () =
    openedOn
        { Remembered = []
          Last = LastBytes None }

let private lastBytes raw direction : ByteSearch.Query =
    { Bytes = WantedBytes.tryParse raw |> Expect.ok
      Direction = direction }

[<Fact>]
let ``searching bytes greys out every text option`` () =
    use dialog = freshBytes ()

    let choosable = enabled dialog

    Assert.Equal<ViewerSearchDialog.SearchBox list>([ ViewerSearchDialog.BackwardsBox ], choosable)

[<Fact>]
let ``bytes asked again hold the last bytes and direction`` () =
    use dialog =
        openedOn
            { Remembered = []
              Last = LastBytes(Some(lastBytes "4d5a" Backwards)) }

    let text = (ViewerSearchDialog.field dialog).Text
    let checkedBoxes = ticked dialog

    Assert.Equal("4D 5A", text)
    Assert.Equal<ViewerSearchDialog.SearchBox list>([ ViewerSearchDialog.BackwardsBox ], checkedBoxes)

[<Fact>]
let ``the field for bytes offers the bytes searched before`` () =
    use dialog =
        openedOn
            { Remembered = [ typed "4D 5A" ]
              Last = LastBytes None }

    let offered = listed dialog

    Assert.Equal<string list>([ "4D 5A" ], offered)

[<Fact>]
let ``a confirmed popup for bytes yields them as bytes`` () =
    use dialog = freshBytes ()
    (ViewerSearchDialog.field dialog).Text <- "4D 5A"
    tick dialog ViewerSearchDialog.BackwardsBox
    dialog.Result <- AcceptingButton

    let answer = ViewerSearchDialog.answer dialog

    Assert.Equal(
        Some
            { plainAnswer "4D 5A" with
                TypedAs = AsBytes
                Direction = Backwards },
        answer
    )

[<Fact>]
let ``confirming a field that is not bytes refuses it and stays open`` () =
    let refusals = ResizeArray<SearchRefusal>()

    use dialog =
        openedRefusingInto
            refusals
            { Remembered = []
              Last = LastBytes None }

    (ViewerSearchDialog.field dialog).Text <- "MZ"

    confirm dialog

    Assert.Equal(BrokenBytes(NotAByte "MZ"), Assert.Single refusals)
    Assert.False(dialog.Result.HasValue, $"the popup closed with {dialog.Result}")

[<Fact>]
let ``confirming bytes refuses nothing`` () =
    let refusals = ResizeArray<SearchRefusal>()

    use dialog =
        openedRefusingInto
            refusals
            { Remembered = []
              Last = LastBytes None }

    (ViewerSearchDialog.field dialog).Text <- "4D 5A"

    confirm dialog

    Assert.Empty refusals
