module Marbas.Tests.Integration.Dialogs.HistoryDialogForgetTests

open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Profile
open Marbas.State.AppState
open Marbas.Ui.Dialogs
open Marbas.Tests
open Marbas.Tests.Integration.Dialogs.HistoryDialogFixture

let private at = Root.at

let private fp = FileSystemPath.create

let private cursorDown (dialog: HistoryDialog.HistoryView) tab =
    (HistoryDialog.table tab dialog).NewKeyDownEvent Key.CursorDown |> ignore

[<Fact>]
let ``Del forgets the row under the cursor and the popup stays open`` () =
    use dialog = laidOut folders FolderHistoryTab
    cursorDown dialog FolderHistoryTab

    dialog.NewKeyDownEvent Key.Delete |> ignore

    Assert.Equal<string list>([ "14:02 " + at "newer" ], rows dialog FolderHistoryTab)
    Assert.False dialog.Result.HasValue

[<Fact>]
let ``Del hands the file the directory the row stands for`` () =
    let file = HistoryFile folders
    use dialog = openedOver agreeing file FolderHistoryTab
    cursorDown dialog FolderHistoryTab

    dialog.NewKeyDownEvent Key.Delete |> ignore

    Assert.Equal<HistoryChange list>([ Forget(Visited(Place.OnDisk(fp (at "older")))) ], List.ofSeq file.Changes)

[<Fact>]
let ``Del on a file started forgets that start`` () =
    let file = HistoryFile launches
    use dialog = openedOver agreeing file RunHistoryTab

    dialog.NewKeyDownEvent Key.Delete |> ignore

    Assert.Equal<HistoryChange list>([ Forget(Launched(started "work/newer.md")) ], List.ofSeq file.Changes)

[<Fact>]
let ``Del on a file viewed forgets that visit`` () =
    let file = HistoryFile visits
    use dialog = openedOver agreeing file ViewHistoryTab

    dialog.NewKeyDownEvent Key.Delete |> ignore

    Assert.Equal<HistoryChange list>([ Forget(Opened(visitOf "work/typed.md" Edited)) ], List.ofSeq file.Changes)

[<Fact>]
let ``the cursor stays on the row that took the forgotten one's place`` () =
    use dialog = laidOut threeFolders FolderHistoryTab
    cursorDown dialog FolderHistoryTab
    dialog.NewKeyDownEvent Key.Delete |> ignore
    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal(Some(OpenPlace(Place.OnDisk(fp (at "older")))), answer.Choice)

[<Fact>]
let ``the cursor on the last row forgotten moves to the new last one`` () =
    use dialog = laidOut threeFolders FolderHistoryTab
    cursorDown dialog FolderHistoryTab
    cursorDown dialog FolderHistoryTab
    dialog.NewKeyDownEvent Key.Delete |> ignore
    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal(Some(OpenPlace(Place.OnDisk(fp (at "newer")))), answer.Choice)

[<Fact>]
let ``the Delete button forgets the row without closing the popup`` () =
    use dialog = laidOut folders FolderHistoryTab

    (HistoryDialog.button HistoryDialog.DeleteButton dialog).InvokeCommand Command.Accept
    |> ignore

    Assert.Equal<string list>([ "14:01 " + at "older" ], rows dialog FolderHistoryTab)
    Assert.False dialog.Result.HasValue

[<Fact>]
let ``a change the file refused leaves the rows as they were`` () =
    use dialog = openedWith agreeing (fun _ -> None) folders FolderHistoryTab

    dialog.NewKeyDownEvent Key.Delete |> ignore

    Assert.Equal<string list>([ "14:02 " + at "newer"; "14:01 " + at "older" ], rows dialog FolderHistoryTab)

[<Fact>]
let ``deleting is not offered on an empty list`` () =
    use dialog = laidOut folders RunHistoryTab

    let delete = HistoryDialog.button HistoryDialog.DeleteButton dialog

    Assert.False delete.Enabled

[<Fact>]
let ``deleting is offered once a tab with rows is in front`` () =
    use dialog = laidOut folders RunHistoryTab

    dialog.NewKeyDownEvent(Key.PageUp.WithCtrl) |> ignore

    Assert.True (HistoryDialog.button HistoryDialog.DeleteButton dialog).Enabled

[<Fact>]
let ``deleting is no longer offered once the last row is forgotten`` () =
    use dialog = laidOut folders FolderHistoryTab
    dialog.NewKeyDownEvent Key.Delete |> ignore

    dialog.NewKeyDownEvent Key.Delete |> ignore

    Assert.False (HistoryDialog.button HistoryDialog.DeleteButton dialog).Enabled

[<Fact>]
let ``Del on an empty list hands the file nothing`` () =
    let file = HistoryFile History.empty
    use dialog = openedOver agreeing file FolderHistoryTab

    dialog.NewKeyDownEvent Key.Delete |> ignore

    Assert.Empty file.Changes

[<Fact>]
let ``Shift+Del asks before clearing and names the tab`` () =
    let asked = ResizeArray<string>()

    let confirm (_: string) (question: string) =
        asked.Add question
        true

    use dialog = openedOver confirm (HistoryFile folders) FolderHistoryTab

    dialog.NewKeyDownEvent Key.Delete.WithShift |> ignore

    Assert.Contains("Folder history", Assert.Single asked)

[<Fact>]
let ``Shift+Del confirmed clears the tab and the popup stays open`` () =
    let file = HistoryFile folders
    use dialog = openedOver agreeing file FolderHistoryTab

    dialog.NewKeyDownEvent Key.Delete.WithShift |> ignore

    Assert.Equal<HistoryChange list>([ ClearList FolderHistory ], List.ofSeq file.Changes)
    Assert.Empty(rows dialog FolderHistoryTab)
    Assert.False dialog.Result.HasValue

[<Fact>]
let ``Shift+Del on the tab of files started clears the files started`` () =
    let file = HistoryFile launches
    use dialog = openedOver agreeing file RunHistoryTab

    dialog.NewKeyDownEvent Key.Delete.WithShift |> ignore

    Assert.Equal<HistoryChange list>([ ClearList LaunchHistory ], List.ofSeq file.Changes)

[<Fact>]
let ``Shift+Del on the tab of files viewed clears the files viewed and edited`` () =
    let file = HistoryFile visits
    use dialog = openedOver agreeing file ViewHistoryTab

    dialog.NewKeyDownEvent Key.Delete.WithShift |> ignore

    Assert.Equal<HistoryChange list>([ ClearList FileHistory ], List.ofSeq file.Changes)

[<Fact>]
let ``Shift+Del turned down hands the file nothing`` () =
    let file = HistoryFile folders
    use dialog = openedOver (fun _ _ -> false) file FolderHistoryTab

    dialog.NewKeyDownEvent Key.Delete.WithShift |> ignore

    Assert.Empty file.Changes
    Assert.False dialog.Result.HasValue

[<Fact>]
let ``Shift+Del on an empty list asks nothing`` () =
    let asked = ResizeArray<string>()

    let confirm (_: string) (question: string) =
        asked.Add question
        true

    use dialog = openedOver confirm (HistoryFile History.empty) FolderHistoryTab

    dialog.NewKeyDownEvent Key.Delete.WithShift |> ignore

    Assert.Empty asked

[<Fact>]
let ``forgetting the top row leaves the cursor on the next one`` () =
    use dialog = laidOut folders FolderHistoryTab
    dialog.NewKeyDownEvent Key.Delete |> ignore
    dialog.Result <- HistoryDialog.OpenButton

    let answer = HistoryDialog.answer dialog

    Assert.Equal(Some(OpenPlace(Place.OnDisk(fp (at "older")))), answer.Choice)

let private manyFolders =
    [ 1..40 ]
    |> List.fold
        (fun history index ->
            History.apply Windows (minute index) (Remember(Visited(Place.OnDisk(fp (at $"dir{index}"))))) history)
        History.empty

[<Fact>]
let ``a row forgotten below the fold keeps the cursor in sight`` () =
    use dialog = laidOut manyFolders FolderHistoryTab
    let shown = HistoryDialog.table FolderHistoryTab dialog
    shown.SetSelection(0, 30, false, null)
    shown.EnsureCursorIsVisible()

    dialog.NewKeyDownEvent Key.Delete |> ignore

    Assert.InRange(shown.RowOffset, 30 - shown.Viewport.Height + 1, 30)
