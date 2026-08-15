module Marbas.Ui.PanelInput

open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update

let refreshKey = Key.R.WithCtrl

let selectionKeys = [ Key.InsertChar; Key.Space ]

let selectPatternKey = Key '+'

let deselectPatternKey = Key '-'

let leftDriveKey = Key.F1.WithAlt

let rightDriveKey = Key.F2.WithAlt

let nameSortKey = Key.F3.WithCtrl

let extensionSortKey = Key.F4.WithCtrl

let dateSortKey = Key.F5.WithCtrl

let sizeSortKey = Key.F6.WithCtrl

let sortKeys =
    [ nameSortKey, SortKey.ByName
      extensionSortKey, SortKey.ByExtension
      dateSortKey, SortKey.ByDate
      sizeSortKey, SortKey.BySize ]

let private driveSide (key: Key) =
    if key = leftDriveKey then Some Left
    elif key = rightDriveKey then Some Right
    else None

let private sortKey (key: Key) =
    sortKeys |> List.tryPick (fun (bound, sort) -> if bound = key then Some sort else None)

let messageFor (cursor: PanelCursor) (key: Key) : Message option =
    if key = refreshKey then Some(RefreshRequested cursor)
    elif selectionKeys |> List.contains key then Some(SelectionToggled cursor)
    elif key = selectPatternKey then Some(PatternRequested(cursor, Select))
    elif key = deselectPatternKey then Some(PatternRequested(cursor, Deselect))
    else
        match sortKey key with
        | Some sort -> Some(SortRequested(cursor, sort))
        | None -> driveSide key |> Option.map DriveRequested
