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

let messageFor (cursor: PanelCursor) (key: Key) : Message option =
    if key = refreshKey then Some(RefreshRequested cursor)
    elif selectionKeys |> List.contains key then Some(SelectionToggled cursor)
    elif key = selectPatternKey then Some(PatternRequested(cursor, Select))
    elif key = deselectPatternKey then Some(PatternRequested(cursor, Deselect))
    else driveSide key |> Option.map DriveRequested
