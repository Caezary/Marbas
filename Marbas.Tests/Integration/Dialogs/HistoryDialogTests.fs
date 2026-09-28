module Marbas.Tests.Integration.Dialogs.HistoryDialogTests

open System
open System.Drawing
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private now = DateTimeOffset(2026, 9, 28, 15, 30, 0, TimeSpan.FromHours 2.0)

let private minute m =
    DateTimeOffset(2026, 9, 28, 14, m, 0, TimeSpan.FromHours 2.0)

let private folders =
    History.empty
    |> History.apply Windows (minute 1) (Remember(Visited(Place.OnDisk(fp (at "older")))))
    |> History.apply Windows (minute 2) (Remember(Visited(Place.OnDisk(fp (at "newer")))))

let private laidOut history tab =
    DialogStyle.apply ()

    let dialog = HistoryDialog.create (MockFileSystem()) history tab now

    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(100, 30)) |> ignore
    dialog

let private rows (dialog: HistoryDialog.HistoryView) tab =
    let table = (HistoryDialog.table tab dialog).Table
    [ for row in 0 .. table.Rows - 1 -> $"{table[row, 0]} {table[row, 1]}" ]

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
let ``opening a directory answers the one under the cursor`` () =
    use dialog = laidOut folders FolderHistoryTab
    (HistoryDialog.table FolderHistoryTab dialog).NewKeyDownEvent Key.CursorDown |> ignore
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

    Assert.Equal({ Tab = FolderHistoryTab; Choice = None }, answer)

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
let ``going to a directory's entry means nothing on the directory tab`` () =
    use dialog = laidOut folders FolderHistoryTab

    let goTo = HistoryDialog.button HistoryDialog.GoToButton dialog

    Assert.False goTo.Enabled

[<Fact>]
let ``Alt+G on the directory tab answers nothing`` () =
    use dialog = laidOut folders FolderHistoryTab

    dialog.NewKeyDownEvent(Key.G.WithAlt) |> ignore

    Assert.False dialog.Result.HasValue
