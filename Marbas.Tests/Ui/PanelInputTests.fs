module Marbas.Tests.Ui.PanelInputTests

open Xunit
open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui

let private cursor side index = { Side = side; Index = index }

[<Fact>]
let ``the refresh key requests a refresh carrying side and cursor`` () =
    let message = PanelInput.messageFor (cursor Left 3) PanelInput.refreshKey

    Assert.Equal(Some(RefreshRequested(cursor Left 3)), message)

[<Fact>]
let ``every selection key toggles the selection at the cursor`` () =
    for key in PanelInput.selectionKeys do
        let message = PanelInput.messageFor (cursor Right 5) key

        Assert.Equal(Some(SelectionToggled(cursor Right 5)), message)

[<Fact>]
let ``the select key requests a selecting pattern`` () =
    let message = PanelInput.messageFor (cursor Left 2) PanelInput.selectPatternKey

    Assert.Equal(Some(PatternRequested(cursor Left 2, Select)), message)

[<Fact>]
let ``the deselect key requests a deselecting pattern`` () =
    let message = PanelInput.messageFor (cursor Left 2) PanelInput.deselectPatternKey

    Assert.Equal(Some(PatternRequested(cursor Left 2, Deselect)), message)

[<Fact>]
let ``a drive key targets its own side regardless of the focused panel`` () =
    let fromRight = PanelInput.messageFor (cursor Right 0) PanelInput.leftDriveKey
    let fromLeft = PanelInput.messageFor (cursor Left 0) PanelInput.rightDriveKey

    Assert.Equal(Some(DriveRequested Left), fromRight)
    Assert.Equal(Some(DriveRequested Right), fromLeft)

[<Fact>]
let ``the sort keys are the classic Ctrl+F3 to Ctrl+F6`` () =
    let bindings = PanelInput.sortKeys

    Assert.Equal<(Key * SortKey) list>(
        [ Key.F3.WithCtrl, SortKey.ByName
          Key.F4.WithCtrl, SortKey.ByExtension
          Key.F5.WithCtrl, SortKey.ByDate
          Key.F6.WithCtrl, SortKey.BySize ],
        bindings
    )

[<Fact>]
let ``a sort key requests its own sort at the cursor`` () =
    let message = PanelInput.messageFor (cursor Right 4) PanelInput.dateSortKey

    Assert.Equal(Some(SortRequested(cursor Right 4, SortKey.ByDate)), message)

[<Fact>]
let ``an unmapped key produces no message`` () =
    let message = PanelInput.messageFor (cursor Left 0) (Key 'z')

    Assert.Equal(None, message)
