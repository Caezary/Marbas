module Marbas.Tests.PanelInputTests

open Xunit
open Terminal.Gui.Input
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Ui

[<Fact>]
let ``the refresh key requests a refresh carrying side and cursor`` () =
    let message = PanelInput.messageFor Left 3 PanelInput.refreshKey

    Assert.Equal(Some(RefreshRequested(Left, 3)), message)

[<Fact>]
let ``every selection key toggles the selection at the cursor`` () =
    for key in PanelInput.selectionKeys do
        let message = PanelInput.messageFor Right 5 key

        Assert.Equal(Some(SelectionToggled(Right, 5)), message)

[<Fact>]
let ``the select key requests a selecting pattern`` () =
    let message = PanelInput.messageFor Left 2 PanelInput.selectPatternKey

    Assert.Equal(Some(PatternRequested(Left, 2, true)), message)

[<Fact>]
let ``the deselect key requests a deselecting pattern`` () =
    let message = PanelInput.messageFor Left 2 PanelInput.deselectPatternKey

    Assert.Equal(Some(PatternRequested(Left, 2, false)), message)

[<Fact>]
let ``a drive key targets its own side regardless of the focused panel`` () =
    let fromRight = PanelInput.messageFor Right 0 PanelInput.leftDriveKey
    let fromLeft = PanelInput.messageFor Left 0 PanelInput.rightDriveKey

    Assert.Equal(Some(DriveRequested Left), fromRight)
    Assert.Equal(Some(DriveRequested Right), fromLeft)

[<Fact>]
let ``an unmapped key produces no message`` () =
    let message = PanelInput.messageFor Left 0 (Key 'z')

    Assert.Equal(None, message)
