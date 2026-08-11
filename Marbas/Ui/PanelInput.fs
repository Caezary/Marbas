module Marbas.Ui.PanelInput

open Terminal.Gui.Input
open Marbas.State.AppState
open Marbas.State.Update

let refreshKey = Key.R.WithCtrl

let selectionKeys = [ Key.InsertChar; Key.Space ]

let selectPatternKey = Key '+'

let deselectPatternKey = Key '-'

let leftDriveKey = Key.F1.WithAlt

let rightDriveKey = Key.F2.WithAlt

let private driveSide (key: Key) =
    if key = leftDriveKey then Some Left
    elif key = rightDriveKey then Some Right
    else None

let messageFor (side: PanelSide) (cursor: int) (key: Key) : Message option =
    if key = refreshKey then Some(RefreshRequested(side, cursor))
    elif selectionKeys |> List.contains key then Some(SelectionToggled(side, cursor))
    elif key = selectPatternKey then Some(PatternRequested(side, cursor, Select))
    elif key = deselectPatternKey then Some(PatternRequested(side, cursor, Deselect))
    else driveSide key |> Option.map DriveRequested
