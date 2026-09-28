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

let private started target =
    { Target = fp (at target)
      Program = ProgramPath.tryCreate "code.exe"
      WorkingDirectory = fp (at "work")
      Elevation = AsAdministrator }

let private launches =
    History.empty
    |> History.apply Windows (minute 3) (Remember(Launched(started "work/older.md")))
    |> History.apply Windows (minute 4) (Remember(Launched(started "work/newer.md")))

let private cells (dialog: HistoryDialog.HistoryView) tab =
    let table = (HistoryDialog.table tab dialog).Table
    [ for row in 0 .. table.Rows - 1 -> [ for column in 0 .. table.Columns - 1 -> string table[row, column] ] ]

[<Fact>]
let ``the files started are listed with how they were started`` () =
    use dialog = laidOut launches RunHistoryTab

    let listed = cells dialog RunHistoryTab

    Assert.Equal<string list list>(
        [ [ "14:04"; at "work/newer.md"; "code.exe, as administrator" ]
          [ "14:03"; at "work/older.md"; "code.exe, as administrator" ] ],
        listed
    )

[<Fact>]
let ``opening a started file answers starting it again the same way`` () =
    use dialog = laidOut launches RunHistoryTab
    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal({ Tab = RunHistoryTab; Choice = Some(RunAgain(started "work/newer.md")) }, answer)

[<Fact>]
let ``Alt+G on a started file answers going to it`` () =
    use dialog = laidOut launches RunHistoryTab

    dialog.NewKeyDownEvent(Key.G.WithAlt) |> ignore

    let answer = HistoryDialog.answer dialog
    Assert.Equal(Nullable HistoryDialog.GoToButton, dialog.Result)
    Assert.Equal({ Tab = RunHistoryTab; Choice = Some(GoTo(Place.OnDisk(fp (at "work/newer.md")))) }, answer)

[<Fact>]
let ``going to a started file is offered once its tab is in front`` () =
    use dialog = laidOut launches FolderHistoryTab

    dialog.NewKeyDownEvent(Key.PageDown.WithCtrl) |> ignore

    Assert.True (HistoryDialog.button HistoryDialog.GoToButton dialog).Enabled

[<Fact>]
let ``going to anything is not offered on an empty list`` () =
    use dialog = laidOut History.empty RunHistoryTab

    let goTo = HistoryDialog.button HistoryDialog.GoToButton dialog

    Assert.False goTo.Enabled

[<Fact>]
let ``Alt+G on the directory tab answers nothing`` () =
    use dialog = laidOut folders FolderHistoryTab

    dialog.NewKeyDownEvent(Key.G.WithAlt) |> ignore

    Assert.False dialog.Result.HasValue

let private visits =
    History.empty
    |> History.apply Windows (minute 5) (Remember(Opened { Where = Place.OnDisk(fp (at "work/seen.md")); How = Viewed }))
    |> History.apply Windows (minute 6) (Remember(Opened { Where = Place.OnDisk(fp (at "work/typed.md")); How = Edited }))

let private visitOf path how =
    { Where = Place.OnDisk(fp (at path)); How = how }

let private onSecondRow (dialog: HistoryDialog.HistoryView) =
    (HistoryDialog.table ViewHistoryTab dialog).NewKeyDownEvent Key.CursorDown |> ignore
    dialog

[<Fact>]
let ``the files viewed and edited are marked with how they were used`` () =
    use dialog = laidOut visits ViewHistoryTab

    let listed = cells dialog ViewHistoryTab

    Assert.Equal<string list list>(
        [ [ "14:06"; at "work/typed.md"; "E" ]; [ "14:05"; at "work/seen.md"; "V" ] ],
        listed
    )

[<Fact>]
let ``opening an edited file answers editing it again`` () =
    use dialog = laidOut visits ViewHistoryTab
    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal({ Tab = ViewHistoryTab; Choice = Some(Revisit(visitOf "work/typed.md" Edited)) }, answer)

[<Fact>]
let ``opening a viewed file answers viewing it again`` () =
    use dialog = laidOut visits ViewHistoryTab |> onSecondRow
    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal({ Tab = ViewHistoryTab; Choice = Some(Revisit(visitOf "work/seen.md" Viewed)) }, answer)

[<Fact>]
let ``F3 on an edited file answers viewing it`` () =
    use dialog = laidOut visits ViewHistoryTab

    dialog.NewKeyDownEvent Key.F3 |> ignore

    let answer = HistoryDialog.answer dialog
    Assert.Equal({ Tab = ViewHistoryTab; Choice = Some(Revisit(visitOf "work/typed.md" Viewed)) }, answer)

[<Fact>]
let ``F4 on a viewed file answers editing it`` () =
    use dialog = laidOut visits ViewHistoryTab |> onSecondRow

    dialog.NewKeyDownEvent Key.F4 |> ignore

    let answer = HistoryDialog.answer dialog
    Assert.Equal({ Tab = ViewHistoryTab; Choice = Some(Revisit(visitOf "work/seen.md" Edited)) }, answer)

[<Fact>]
let ``Alt+G on a file viewed answers going to it`` () =
    use dialog = laidOut visits ViewHistoryTab

    dialog.NewKeyDownEvent(Key.G.WithAlt) |> ignore

    let answer = HistoryDialog.answer dialog
    Assert.Equal({ Tab = ViewHistoryTab; Choice = Some(GoTo(Place.OnDisk(fp (at "work/typed.md")))) }, answer)

[<Fact>]
let ``F3 on the directory tab leaves the popup open`` () =
    use dialog = laidOut folders FolderHistoryTab

    dialog.NewKeyDownEvent Key.F3 |> ignore

    Assert.False dialog.Result.HasValue

[<Fact>]
let ``F4 on an empty list of files leaves the popup open`` () =
    use dialog = laidOut History.empty ViewHistoryTab

    dialog.NewKeyDownEvent Key.F4 |> ignore

    Assert.False dialog.Result.HasValue
