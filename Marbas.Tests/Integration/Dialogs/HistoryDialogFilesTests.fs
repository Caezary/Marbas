module Marbas.Tests.Integration.Dialogs.HistoryDialogFilesTests

open System
open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.Profile
open Marbas.State.AppState
open Marbas.Ui.Dialogs
open Marbas.Tests
open Marbas.Tests.Integration.Dialogs.HistoryDialogFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``the files started are listed with how they were started`` () =
    use dialog = laidOut launches RunHistoryTab

    let listed = cells dialog RunHistoryTab

    Assert.Equal<string list list>(
        [ [ "14:04"; at "work/newer.md" + " (code.exe)"; "admin" ]
          [ "14:03"; at "work/older.md" + " (code.exe)"; "admin" ] ],
        listed
    )

[<Fact>]
let ``opening a started file answers starting it again the same way`` () =
    use dialog = laidOut launches RunHistoryTab
    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal(
        { Tab = RunHistoryTab
          Choice = Some(RunAgain(StartedFile(started "work/newer.md"))) },
        answer
    )

[<Fact>]
let ``Alt+G on a started file answers going to it`` () =
    use dialog = laidOut launches RunHistoryTab

    dialog.NewKeyDownEvent(Key.G.WithAlt) |> ignore

    let answer = HistoryDialog.answer dialog
    Assert.Equal(Nullable HistoryDialog.GoToButton, dialog.Result)

    Assert.Equal(
        { Tab = RunHistoryTab
          Choice = Some(GoTo(Place.OnDisk(fp (at "work/newer.md")))) },
        answer
    )

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
let ``Alt+G on a directory answers opening it`` () =
    use dialog = laidOut folders FolderHistoryTab

    dialog.NewKeyDownEvent(Key.G.WithAlt) |> ignore

    let answer = HistoryDialog.answer dialog
    Assert.Equal(Some(OpenPlace(Place.OnDisk(fp (at "newer")))), answer.Choice)

let private onSecondRow (dialog: HistoryDialog.HistoryView) =
    (HistoryDialog.table ViewHistoryTab dialog).NewKeyDownEvent Key.CursorDown
    |> ignore

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

    Assert.Equal(
        { Tab = ViewHistoryTab
          Choice = Some(Revisit(visitOf "work/typed.md" Edited)) },
        answer
    )

[<Fact>]
let ``opening a viewed file answers viewing it again`` () =
    use dialog = laidOut visits ViewHistoryTab |> onSecondRow
    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal(
        { Tab = ViewHistoryTab
          Choice = Some(Revisit(visitOf "work/seen.md" Viewed)) },
        answer
    )

[<Fact>]
let ``F3 on an edited file answers viewing it`` () =
    use dialog = laidOut visits ViewHistoryTab

    dialog.NewKeyDownEvent Key.F3 |> ignore

    let answer = HistoryDialog.answer dialog

    Assert.Equal(
        { Tab = ViewHistoryTab
          Choice = Some(Revisit(visitOf "work/typed.md" Viewed)) },
        answer
    )

[<Fact>]
let ``F4 on a viewed file answers editing it`` () =
    use dialog = laidOut visits ViewHistoryTab |> onSecondRow

    dialog.NewKeyDownEvent Key.F4 |> ignore

    let answer = HistoryDialog.answer dialog

    Assert.Equal(
        { Tab = ViewHistoryTab
          Choice = Some(Revisit(visitOf "work/seen.md" Edited)) },
        answer
    )

[<Fact>]
let ``Alt+G on a file viewed answers going to it`` () =
    use dialog = laidOut visits ViewHistoryTab

    dialog.NewKeyDownEvent(Key.G.WithAlt) |> ignore

    let answer = HistoryDialog.answer dialog

    Assert.Equal(
        { Tab = ViewHistoryTab
          Choice = Some(GoTo(Place.OnDisk(fp (at "work/typed.md")))) },
        answer
    )

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
