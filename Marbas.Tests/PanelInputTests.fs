module Marbas.Tests.PanelInputTests

open Xunit
open Terminal.Gui.Input
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui

[<Fact>]
let ``the refresh key requests a refresh carrying side and cursor`` () =
    Assert.Equal(Some(RefreshRequested(Left, 3)), PanelInput.messageFor Left 3 PanelInput.refreshKey)

[<Fact>]
let ``every selection key toggles the selection at the cursor`` () =
    for key in PanelInput.selectionKeys do
        Assert.Equal(Some(SelectionToggled(Right, 5)), PanelInput.messageFor Right 5 key)

[<Fact>]
let ``the select key requests a selecting pattern`` () =
    Assert.Equal(Some(PatternRequested(Left, 2, true)), PanelInput.messageFor Left 2 PanelInput.selectPatternKey)

[<Fact>]
let ``the deselect key requests a deselecting pattern`` () =
    Assert.Equal(Some(PatternRequested(Left, 2, false)), PanelInput.messageFor Left 2 PanelInput.deselectPatternKey)

[<Fact>]
let ``a drive key targets its own side regardless of the focused panel`` () =
    Assert.Equal(Some(DriveRequested Left), PanelInput.messageFor Right 0 PanelInput.leftDriveKey)
    Assert.Equal(Some(DriveRequested Right), PanelInput.messageFor Left 0 PanelInput.rightDriveKey)

[<Fact>]
let ``an unmapped key produces no message`` () =
    Assert.Equal(None, PanelInput.messageFor Left 0 (Key 'z'))
