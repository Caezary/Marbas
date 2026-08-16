module Marbas.Ui.KeyMap

open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update

let refreshKey = Key.R.WithCtrl

let insertKey = Key.InsertChar

let spaceKey = Key.Space

let selectionKeys = [ insertKey; spaceKey ]

let selectPatternKey = Key '+'

let deselectPatternKey = Key '-'

let leftDriveKey = Key.F1.WithAlt

let rightDriveKey = Key.F2.WithAlt

let nameSortKey = Key.F3.WithCtrl

let extensionSortKey = Key.F4.WithCtrl

let dateSortKey = Key.F5.WithCtrl

let sizeSortKey = Key.F6.WithCtrl

let hiddenKey = Key.H.WithAlt

let classicHiddenKey = Key.H.WithCtrl

let hiddenKeys = [ hiddenKey; classicHiddenKey ]

type Binding =
    { Key: Key
      Label: string option
      Message: Message }

let private onBar key label message =
    { Key = key
      Label = Some label
      Message = message }

let private offBar key message =
    { Key = key
      Label = None
      Message = message }

let bindings =
    [ onBar Key.F3 "View" ViewRequested
      onBar Key.F4 "Edit" EditRequested
      onBar Key.F5 "Copy" CopyRequested
      onBar Key.F6 "RenMov" MoveRequested
      onBar Key.F7 "Mkdir" MakeDirectoryRequested
      onBar Key.F8 "Delete" DeleteRequested
      onBar Key.F10 "Quit" QuitRequested
      onBar nameSortKey "Name" (SortRequested SortKey.ByName)
      onBar extensionSortKey "Ext" (SortRequested SortKey.ByExtension)
      onBar dateSortKey "Date" (SortRequested SortKey.ByDate)
      onBar sizeSortKey "Size" (SortRequested SortKey.BySize)
      onBar leftDriveKey "Left" (DriveRequested Left)
      onBar rightDriveKey "Right" (DriveRequested Right)
      onBar hiddenKey "Hidden" HiddenToggled
      offBar classicHiddenKey HiddenToggled
      offBar refreshKey RefreshRequested
      offBar selectPatternKey (PatternRequested Select)
      offBar deselectPatternKey (PatternRequested Deselect)
      offBar insertKey SelectionToggled
      offBar spaceKey SelectionToggled ]

let row modifiers =
    bindings
    |> List.choose (fun binding ->
        match binding.Label with
        | Some label when Modifiers.ofKey binding.Key = modifiers -> Some(binding.Key, label, binding.Message)
        | _ -> None)

let messageFor (key: Key) =
    bindings
    |> List.tryPick (fun binding ->
        if binding.Key = key then
            Some binding.Message
        else
            None)
