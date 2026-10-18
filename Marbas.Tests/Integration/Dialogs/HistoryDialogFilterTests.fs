module Marbas.Tests.Integration.Dialogs.HistoryDialogFilterTests

open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Programs
open Marbas.Profile
open Marbas.State.AppState
open Marbas.Ui.Dialogs
open Marbas.Tests
open Marbas.Tests.Integration.Dialogs.HistoryDialogFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private typing (dialog: HistoryDialog.HistoryView) (text: string) =
    text
    |> Seq.iter (fun character -> dialog.NewKeyDownEvent(Key character) |> ignore)

let private filterShown (dialog: HistoryDialog.HistoryView) = (HistoryDialog.filterLine dialog).Text

[<Fact>]
let ``typing narrows the tab in front to the rows holding what was typed`` () =
    use dialog = laidOut folders FolderHistoryTab

    typing dialog "new"

    Assert.Equal<string list>([ "14:02 " + at "newer" ], rows dialog FolderHistoryTab)

[<Fact>]
let ``typing matches in the middle of a row and ignores case`` () =
    use dialog = laidOut folders FolderHistoryTab

    typing dialog "LDE"

    Assert.Equal<string list>([ "14:01 " + at "older" ], rows dialog FolderHistoryTab)

[<Fact>]
let ``typing matches how a file was started as well as where it is`` () =
    let asUser =
        { started "work/plain.md" with
            Elevation = AsUser }

    let mixed =
        History.empty
        |> History.apply Windows (minute 3) (Remember(Launched(started "work/raised.md")))
        |> History.apply Windows (minute 4) (Remember(Launched asUser))

    use dialog = laidOut mixed RunHistoryTab

    typing dialog "admin"

    Assert.Equal<string list list>(
        [ [ "14:03"; at "work/raised.md" + " (code.exe)"; "admin" ] ],
        cells dialog RunHistoryTab
    )

[<Fact>]
let ``a space is typed into the filter`` () =
    let spaced =
        History.empty
        |> History.apply Windows (minute 1) (Remember(Visited(Place.OnDisk(fp (at "my docs")))))
        |> History.apply Windows (minute 2) (Remember(Visited(Place.OnDisk(fp (at "mydocs")))))

    use dialog = laidOut spaced FolderHistoryTab

    typing dialog "y d"

    Assert.Equal<string list>([ "14:01 " + at "my docs" ], rows dialog FolderHistoryTab)

[<Fact>]
let ``the filter is shown on the line under the table`` () =
    use dialog = laidOut folders FolderHistoryTab

    typing dialog "new"

    Assert.Equal("Filter: new", filterShown dialog)

[<Fact>]
let ``no filter shows an empty line`` () =
    use dialog = laidOut folders FolderHistoryTab

    let shown = filterShown dialog

    Assert.Equal("", shown)

[<Fact>]
let ``Backspace shortens the filter`` () =
    use dialog = laidOut folders FolderHistoryTab
    typing dialog "nex"

    dialog.NewKeyDownEvent Key.Backspace |> ignore

    Assert.Equal("Filter: ne", filterShown dialog)
    Assert.Equal<string list>([ "14:02 " + at "newer" ], rows dialog FolderHistoryTab)

[<Fact>]
let ``Backspace over the last character clears the filter`` () =
    use dialog = laidOut folders FolderHistoryTab
    typing dialog "n"

    dialog.NewKeyDownEvent Key.Backspace |> ignore

    Assert.Equal("", filterShown dialog)
    Assert.Equal<string list>([ "14:02 " + at "newer"; "14:01 " + at "older" ], rows dialog FolderHistoryTab)

[<Fact>]
let ``Esc clears a filter that is not empty and leaves the popup open`` () =
    use dialog = laidOut folders FolderHistoryTab
    typing dialog "new"

    let taken = dialog.NewKeyDownEvent Key.Esc

    Assert.True taken
    Assert.Equal("", filterShown dialog)
    Assert.Equal<string list>([ "14:02 " + at "newer"; "14:01 " + at "older" ], rows dialog FolderHistoryTab)
    Assert.False dialog.Result.HasValue

[<Fact>]
let ``switching tabs clears the filter`` () =
    use dialog = laidOut folders FolderHistoryTab
    typing dialog "new"
    dialog.NewKeyDownEvent(Key.PageDown.WithCtrl) |> ignore

    dialog.NewKeyDownEvent(Key.PageUp.WithCtrl) |> ignore

    Assert.Equal("", filterShown dialog)
    Assert.Equal<string list>([ "14:02 " + at "newer"; "14:01 " + at "older" ], rows dialog FolderHistoryTab)

[<Fact>]
let ``opening a filtered row answers the entry it stands for`` () =
    use dialog = laidOut folders FolderHistoryTab
    typing dialog "old"
    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal(Some(OpenPlace(Place.OnDisk(fp (at "older")))), answer.Choice)

[<Fact>]
let ``Del on a filtered row forgets the entry it stands for`` () =
    let file = HistoryFile folders
    use dialog = openedOver agreeing file FolderHistoryTab
    typing dialog "old"

    dialog.NewKeyDownEvent Key.Delete |> ignore

    Assert.Equal<HistoryChange list>([ Forget(Visited(Place.OnDisk(fp (at "older")))) ], List.ofSeq file.Changes)

[<Fact>]
let ``the filter stays once a row is forgotten`` () =
    use dialog = laidOut threeFolders FolderHistoryTab
    typing dialog "ne"

    dialog.NewKeyDownEvent Key.Delete |> ignore

    Assert.Equal("Filter: ne", filterShown dialog)
    Assert.Equal<string list>([ "14:02 " + at "newer" ], rows dialog FolderHistoryTab)

[<Fact>]
let ``a filter that leaves no row offers nothing to delete or go to`` () =
    use dialog = laidOut launches RunHistoryTab

    typing dialog "zzz"

    Assert.False (HistoryDialog.button HistoryDialog.DeleteButton dialog).Enabled
    Assert.False (HistoryDialog.button HistoryDialog.GoToButton dialog).Enabled

[<Fact>]
let ``F3 on a filter that leaves no file keeps the popup open`` () =
    use dialog = laidOut visits ViewHistoryTab
    typing dialog "zzz"

    dialog.NewKeyDownEvent Key.F3 |> ignore

    Assert.False dialog.Result.HasValue

[<Fact>]
let ``a popup closed with a filter typed is let go cleanly`` () =
    let dialog = laidOut folders FolderHistoryTab
    typing dialog "new"

    let failure = Record.Exception(fun () -> dialog.Dispose())

    Assert.Null failure

[<Fact>]
let ``typing goes on once the filter leaves no row`` () =
    use dialog = laidOut folders FolderHistoryTab
    dialog.SetFocus() |> ignore
    typing dialog "zz"

    typing dialog "q"

    Assert.Equal("Filter: zzq", filterShown dialog)
