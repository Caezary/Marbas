module Marbas.Tests.Integration.Dialogs.HistoryDialogTests

open System
open Xunit
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Marbas.FileSystem
open Marbas.Profile
open Marbas.State.AppState
open Marbas.Ui.Dialogs
open Marbas.Tests
open Marbas.Tests.Integration.Dialogs.HistoryDialogFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``the popup opens on the tab it was asked for`` () =
    use dialog = laidOut folders ViewHistoryTab

    let front = HistoryDialog.front dialog

    Assert.Equal(ViewHistoryTab, front)

[<Fact>]
let ``the directories are listed newest first with the moment they were used`` () =
    use dialog = laidOut folders FolderHistoryTab

    let listed = rows dialog FolderHistoryTab

    Assert.Equal<string list>([ "14:02 " + at "newer"; "14:01 " + at "older" ], listed)

[<Fact>]
let ``Ctrl+PgDn and then Ctrl+PgUp come back to the tab they left`` () =
    use dialog = laidOut folders FolderHistoryTab
    dialog.NewKeyDownEvent(Key.PageDown.WithCtrl) |> ignore

    dialog.NewKeyDownEvent(Key.PageUp.WithCtrl) |> ignore

    Assert.Equal(FolderHistoryTab, HistoryDialog.front dialog)

[<Fact>]
let ``Ctrl+PgDn walks from the last tab round to the first`` () =
    use dialog = laidOut folders FolderHistoryTab

    dialog.NewKeyDownEvent(Key.PageDown.WithCtrl) |> ignore

    Assert.Equal(RunHistoryTab, HistoryDialog.front dialog)

[<Fact>]
let ``Ctrl+PgUp walks to the tab before`` () =
    use dialog = laidOut folders FolderHistoryTab

    dialog.NewKeyDownEvent(Key.PageUp.WithCtrl) |> ignore

    Assert.Equal(ViewHistoryTab, HistoryDialog.front dialog)

[<Fact>]
let ``Right walks to the next tab`` () =
    use dialog = laidOut folders ViewHistoryTab

    dialog.NewKeyDownEvent Key.CursorRight |> ignore

    Assert.Equal(FolderHistoryTab, HistoryDialog.front dialog)

[<Fact>]
let ``Left walks to the tab before`` () =
    use dialog = laidOut folders ViewHistoryTab

    dialog.NewKeyDownEvent Key.CursorLeft |> ignore

    Assert.Equal(RunHistoryTab, HistoryDialog.front dialog)

[<Fact>]
let ``Up on the first row keeps the tab in front`` () =
    use dialog = laidOut folders FolderHistoryTab

    dialog.NewKeyDownEvent Key.CursorUp |> ignore

    Assert.Equal(FolderHistoryTab, HistoryDialog.front dialog)

[<Fact>]
let ``Down on the last row keeps the tab in front`` () =
    use dialog = laidOut folders FolderHistoryTab
    dialog.NewKeyDownEvent Key.CursorDown |> ignore

    dialog.NewKeyDownEvent Key.CursorDown |> ignore

    Assert.Equal(FolderHistoryTab, HistoryDialog.front dialog)

[<Fact>]
let ``Down on an empty list keeps the tab in front`` () =
    use dialog = laidOut History.empty ViewHistoryTab

    dialog.NewKeyDownEvent Key.CursorDown |> ignore

    Assert.Equal(ViewHistoryTab, HistoryDialog.front dialog)

[<Fact>]
let ``Down moves the cursor to the next row`` () =
    use dialog = laidOut folders FolderHistoryTab
    dialog.NewKeyDownEvent Key.CursorDown |> ignore
    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal(Some(OpenPlace(Place.OnDisk(fp (at "older")))), answer.Choice)

[<Theory>]
[<InlineData(100, 22)>]
[<InlineData(52, 18)>]
[<InlineData(30, 16)>]
let ``a tab header takes what room the width leaves, within bounds`` (available: int) (expected: int) =
    let length = HistoryDialog.headerLength available

    Assert.Equal(expected, length)

let private headerLengths (dialog: HistoryDialog.HistoryView) =
    [ RunHistoryTab; ViewHistoryTab; FolderHistoryTab ]
    |> List.map (fun tab -> Expect.notNull (HistoryDialog.table tab dialog).Border.View :?> BorderView)
    |> List.map _.TabLength

[<Fact>]
let ``every tab header is as wide as the others`` () =
    use dialog = laidOut folders FolderHistoryTab

    let lengths = headerLengths dialog

    Assert.Equal<Nullable<int> list>(List.replicate 3 (Nullable 22), lengths)

[<Fact>]
let ``a tab header centres its title`` () =
    use dialog = laidOut folders FolderHistoryTab

    let header = HistoryDialog.header ViewHistoryTab dialog |> Expect.some

    Assert.Equal(Alignment.Center, header.TextAlignment)

[<Fact>]
let ``the header of the tab in front is drawn like the others`` () =
    use dialog = laidOut folders FolderHistoryTab

    let header = HistoryDialog.header FolderHistoryTab dialog |> Expect.some

    Assert.Equal(header.GetAttributeForRole VisualRole.Normal, header.GetAttributeForRole VisualRole.Focus)

let private columnWidth (dialog: HistoryDialog.HistoryView) tab column =
    let style =
        (HistoryDialog.table tab dialog).Style.GetColumnStyleIfAny column
        |> Expect.notNull

    style.MinWidth, style.MaxWidth

[<Fact>]
let ``the When column always holds a whole date`` () =
    use dialog = laidOut folders FolderHistoryTab

    let width = columnWidth dialog FolderHistoryTab 0

    Assert.Equal((10, 10), width)

[<Fact>]
let ``the How column holds no more than the administrator note`` () =
    use dialog = laidOut folders RunHistoryTab

    let width = columnWidth dialog RunHistoryTab 2

    Assert.Equal((5, 5), width)

[<Fact>]
let ``the What column takes the width When and How leave`` () =
    use dialog = laidOut folders ViewHistoryTab
    let shown = (HistoryDialog.table ViewHistoryTab dialog).Viewport.Width

    let width = columnWidth dialog ViewHistoryTab 1

    Assert.Equal((shown - 17, shown - 17), width)

[<Fact>]
let ``the What column of the directory tab takes the width When leaves`` () =
    use dialog = laidOut folders FolderHistoryTab
    let shown = (HistoryDialog.table FolderHistoryTab dialog).Viewport.Width

    let width = columnWidth dialog FolderHistoryTab 1

    Assert.Equal((shown - 11, shown - 11), width)

[<Fact>]
let ``opening a directory answers the one under the cursor`` () =
    use dialog = laidOut folders FolderHistoryTab

    (HistoryDialog.table FolderHistoryTab dialog).NewKeyDownEvent Key.CursorDown
    |> ignore

    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal(
        { Tab = FolderHistoryTab
          Choice = Some(OpenPlace(Place.OnDisk(fp (at "older")))) },
        answer
    )

[<Fact>]
let ``a popup closed without a choice still says which tab it was left on`` () =
    use dialog = laidOut folders FolderHistoryTab
    dialog.NewKeyDownEvent(Key.PageUp.WithCtrl) |> ignore

    let answer = HistoryDialog.answer dialog

    Assert.Equal({ Tab = ViewHistoryTab; Choice = None }, answer)

[<Fact>]
let ``opening on an empty list chooses nothing`` () =
    use dialog = laidOut History.empty FolderHistoryTab
    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal(
        { Tab = FolderHistoryTab
          Choice = None },
        answer
    )

[<Theory>]
[<InlineData('d')>]
[<InlineData('g')>]
[<InlineData('o')>]
[<InlineData('c')>]
let ``a bare letter presses no button`` (letter: char) =
    use dialog = laidOut folders FolderHistoryTab

    dialog.NewKeyDownEvent(Key letter) |> ignore

    Assert.False(dialog.Result.HasValue, $"'{letter}' pressed button {dialog.Result}")

[<Fact>]
let ``going to a directory is offered on the directory tab`` () =
    use dialog = laidOut folders FolderHistoryTab

    let goTo = HistoryDialog.button HistoryDialog.GoToButton dialog

    Assert.True goTo.Enabled

[<Fact>]
let ``going to a directory answers opening it`` () =
    use dialog = laidOut folders FolderHistoryTab

    (HistoryDialog.table FolderHistoryTab dialog).NewKeyDownEvent Key.CursorDown
    |> ignore

    dialog.Result <- HistoryDialog.GoToButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal(
        { Tab = FolderHistoryTab
          Choice = Some(OpenPlace(Place.OnDisk(fp (at "older")))) },
        answer
    )
