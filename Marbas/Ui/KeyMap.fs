module Marbas.Ui.KeyMap

open Terminal.Gui.Input
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update

let functionKeys =
    [ Key.F1
      Key.F2
      Key.F3
      Key.F4
      Key.F5
      Key.F6
      Key.F7
      Key.F8
      Key.F9
      Key.F10
      Key.F11
      Key.F12 ]

let refreshKey = Key.R.WithCtrl

let insertKey = Key.InsertChar

let spaceKey = Key.Space

let selectionDownKey = Key.CursorDown.WithShift

let selectionUpKey = Key.CursorUp.WithShift

let selectionKeys = [ insertKey; spaceKey; selectionDownKey ]

let selectionPageDownKeys = [ Key.CursorRight.WithShift; Key.PageDown.WithShift ]

let selectionPageUpKeys = [ Key.CursorLeft.WithShift; Key.PageUp.WithShift ]

let selectionPageDirection (key: Key) =
    if List.contains key selectionPageDownKeys then Some Downward
    elif List.contains key selectionPageUpKeys then Some Upward
    else None

let selectPatternKey = Key '+'

let deselectPatternKey = Key '-'

let leftRootKey = Key.F1.WithAlt

let rightRootKey = Key.F2.WithAlt

let nameSortKey = Key.F3.WithCtrl

let extensionSortKey = Key.F4.WithCtrl

let dateSortKey = Key.F5.WithCtrl

let sizeSortKey = Key.F6.WithCtrl

let hiddenKey = Key.H.WithAlt

let classicHiddenKey = Key.H.WithCtrl

let hiddenKeys = [ hiddenKey; classicHiddenKey ]

let quitKey = Key.Q.WithCtrl

let cancelKey = Key.Esc

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
      onBar leftRootKey "Left" (RootRequested Left)
      onBar rightRootKey "Right" (RootRequested Right)
      offBar hiddenKey HiddenToggled
      offBar quitKey QuitRequested
      offBar classicHiddenKey HiddenToggled
      offBar refreshKey RefreshRequested
      offBar selectPatternKey (PatternRequested Select)
      offBar deselectPatternKey (PatternRequested Deselect)
      offBar insertKey (SelectionToggled Downward)
      offBar spaceKey (SelectionToggled Downward)
      offBar selectionDownKey (SelectionToggled Downward)
      offBar selectionUpKey (SelectionToggled Upward) ]

type BarSlot =
    { Name: string
      Key: Key
      Label: string
      Message: Message option }

let private commandOn key =
    bindings
    |> List.tryPick (fun binding ->
        match binding.Label with
        | Some label when binding.Key = key -> Some(label, binding.Message)
        | _ -> None)

let bar modifiers =
    functionKeys
    |> List.mapi (fun index functionKey ->
        let key = Modifiers.apply modifiers functionKey
        let command = commandOn key

        { Name = $"F{index + 1}"
          Key = key
          Label = command |> Option.map fst |> Option.defaultValue ""
          Message = command |> Option.map snd })

let messageFor (key: Key) =
    bindings
    |> List.tryPick (fun binding ->
        if binding.Key = key then
            Some binding.Message
        else
            None)
