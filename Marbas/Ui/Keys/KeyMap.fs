module Marbas.Ui.Keys.KeyMap

open Terminal.Gui.Input
open Marbas.Domain
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

let markExtensionKey = Key.E.WithAlt

let unmarkExtensionKey = Key.E.WithAlt.WithShift

let classicMarkExtensionKey = selectPatternKey.WithCtrl

let classicUnmarkExtensionKey = deselectPatternKey.WithCtrl

let markStemKey = Key.N.WithAlt

let unmarkStemKey = Key.N.WithAlt.WithShift

let classicMarkStemKey = selectPatternKey.WithAlt

let classicUnmarkStemKey = deselectPatternKey.WithAlt

let markFilesKey = Key.A.WithAlt

let unmarkFilesKey = Key.A.WithAlt.WithShift

let classicMarkFilesKey = selectPatternKey.WithShift

let classicUnmarkFilesKey = deselectPatternKey.WithShift

let invertKey = Key.I.WithAlt

let starInvertKey = Key '*'

let classicInvertKey = starInvertKey.WithCtrl

let recallMarksKey = Key.M.WithAlt

let classicRecallMarksKey = Key.M.WithCtrl

let leftRootKey = Key.F1.WithAlt

let rightRootKey = Key.F2.WithAlt

let volumeRootKey = Key.D4.WithCtrl

let classicVolumeRootKey = (Key '\\').WithCtrl

let nameSortKey = Key.F3.WithCtrl

let extensionSortKey = Key.F4.WithCtrl

let dateSortKey = Key.F5.WithCtrl

let sizeSortKey = Key.F6.WithCtrl

let newFileKey = Key.F4.WithShift

let packKey = Key.F1.WithShift

let unpackKey = Key.F2.WithShift

let linkKey = Key.F6.WithAlt

let findKey = Key.F7.WithAlt

let spareFindKey = Key.F.WithAlt.WithShift

let settingsKey = Key.F12

let hiddenKey = Key.H.WithAlt

let classicHiddenKey = Key.H.WithCtrl

let hiddenKeys = [ hiddenKey; classicHiddenKey ]

let trashKey = Key.Delete

let permanentDeleteKey = Key.Delete.WithShift

let wipeKey = Key.Delete.WithAlt

let walkIntoKey = Key.W.WithAlt

let classicWalkIntoKey = Key.PageDown.WithCtrl

let upOneLevelKey = Key.PageUp.WithCtrl

let spareUpOneLevelKey = Key.U.WithAlt

let openWithKey = Key.R.WithAlt

let classicOpenWithKey = Key.Enter.WithShift

let elevatedRunKey = Key.R.WithAlt.WithShift

let classicElevatedRunKey = Key.Enter.WithCtrl.WithAlt

let tabsKey = Key.F11

let spareTabsKey = Key.T.WithAlt

let nextTabKey = Key.CursorRight.WithAlt

let previousTabKey = Key.CursorLeft.WithAlt

let newTabKey = Key.F11.WithAlt

let closeTabKey = Key.F11.WithAlt.WithShift

let saveSessionKey = Key.F9.WithShift

let keepFavoriteKey = Key.K.WithAlt

let favoritesKey = Key.F8.WithAlt

let spareFavoritesKey = Key.B.WithAlt

let favoritesGoKey = Key.Enter

let favoritesKeepKey = Key.InsertChar

let favoritesEmptyKey = Key.Delete

let private digitKey slot =
    Key(char (int '0' + FavoriteSlot.value slot))

let favoriteKey slot = (digitKey slot).WithAlt

let favoriteSlotOf (key: Key) =
    FavoriteSlot.all
    |> List.tryFind (fun slot -> key = digitKey slot || key = favoriteKey slot)

let copyNamesKey = Key.InsertChar.WithCtrl

let spareCopyNamesKey = Key.C.WithAlt

let copyPathsKey = Key.InsertChar.WithAlt.WithShift

let spareCopyPathsKey = Key.P.WithAlt

let copyLinkTargetsKey = Key.InsertChar.WithCtrl.WithAlt

let spareCopyLinkTargetsKey = Key.L.WithAlt

let clipboardPathKey = Key.InsertChar.WithCtrl.WithShift

let spareClipboardPathKey = Key.J.WithAlt

let historyKey = Key.F12.WithAlt

let spareHistoryKey = Key.H.WithAlt.WithShift

let nextHistoryTabKey = Key.PageDown.WithCtrl

let previousHistoryTabKey = Key.PageUp.WithCtrl

let nextHistoryTabArrow = Key.CursorRight

let previousHistoryTabArrow = Key.CursorLeft

let historyRowUpKey = Key.CursorUp

let historyRowDownKey = Key.CursorDown

let historyGoToKey = Key.G.WithAlt

let historyViewKey = Key.F3

let historyEditKey = Key.F4

let historyForgetKey = Key.Delete

let historyClearKey = Key.Delete.WithShift

let quitKey = Key.Q.WithCtrl

let cancelKey = Key.Esc

let private unmodified (key: Key) = not key.IsCtrl && not key.IsAlt

let private visibleCharacter (key: Key) =
    key.AsRune.Value >= 32 && key.AsRune.Value <> 127

let printable key = unmodified key && visibleCharacter key

type Binding =
    { Key: Key
      Label: string option
      Message: Message }

let private labelled key label message =
    { Key = key
      Label = Some label
      Message = message }

let private unlabelled key message =
    { Key = key
      Label = None
      Message = message }

let bindings =
    [ labelled Key.F3 "View" ViewRequested
      labelled Key.F4 "Edit" EditRequested
      labelled Key.F5 "Copy" CopyRequested
      labelled Key.F6 "RenMov" MoveRequested
      labelled Key.F7 "Mkdir" MakeDirectoryRequested
      labelled Key.F8 "Delete" (DeleteRequested ToTrash)
      labelled packKey "Pack" PackRequested
      labelled unpackKey "Unpack" UnpackRequested
      labelled newFileKey "NewFile" NewFileRequested
      labelled Key.F8.WithShift "Erase" (DeleteRequested Permanently)
      labelled tabsKey "Tabs" TabsRequested
      labelled newTabKey "NewTab" TabOpened
      labelled closeTabKey "ShutTab" TabClosed
      labelled saveSessionKey "Save" SessionSaveRequested
      labelled Key.F10 "Quit" QuitRequested
      labelled settingsKey "Setup" SettingsRequested
      labelled nameSortKey "Name" (SortRequested SortKey.ByName)
      labelled extensionSortKey "Ext" (SortRequested SortKey.ByExtension)
      labelled dateSortKey "Date" (SortRequested SortKey.ByDate)
      labelled sizeSortKey "Size" (SortRequested SortKey.BySize)
      labelled leftRootKey "Left" (RootRequested Left)
      labelled rightRootKey "Right" (RootRequested Right)
      labelled linkKey "Link" LinkRequested
      labelled findKey "Find" FindRequested
      unlabelled spareFindKey FindRequested
      labelled historyKey "History" HistoryRequested
      labelled favoritesKey "Favs" FavoritesRequested
      unlabelled spareFavoritesKey FavoritesRequested
      unlabelled spareHistoryKey HistoryRequested
      unlabelled trashKey (DeleteRequested ToTrash)
      unlabelled permanentDeleteKey (DeleteRequested Permanently)
      unlabelled wipeKey (DeleteRequested ByWiping)
      unlabelled hiddenKey HiddenToggled
      unlabelled quitKey QuitRequested
      unlabelled classicHiddenKey HiddenToggled
      unlabelled refreshKey RefreshRequested
      unlabelled volumeRootKey VolumeRootRequested
      unlabelled classicVolumeRootKey VolumeRootRequested
      unlabelled spareTabsKey TabsRequested
      unlabelled nextTabKey (TabCycled Forward)
      unlabelled previousTabKey (TabCycled Backward)
      unlabelled walkIntoKey WalkIntoRequested
      unlabelled classicWalkIntoKey WalkIntoRequested
      unlabelled upOneLevelKey UpOneLevelRequested
      unlabelled spareUpOneLevelKey UpOneLevelRequested
      unlabelled openWithKey OpenWithRequested
      unlabelled classicOpenWithKey OpenWithRequested
      unlabelled elevatedRunKey ElevatedRunRequested
      unlabelled classicElevatedRunKey ElevatedRunRequested
      unlabelled selectPatternKey (PatternRequested Select)
      unlabelled deselectPatternKey (PatternRequested Deselect)
      unlabelled markExtensionKey (LikenessRequested(Select, SameExtension))
      unlabelled unmarkExtensionKey (LikenessRequested(Deselect, SameExtension))
      unlabelled classicMarkExtensionKey (LikenessRequested(Select, SameExtension))
      unlabelled classicUnmarkExtensionKey (LikenessRequested(Deselect, SameExtension))
      unlabelled markStemKey (LikenessRequested(Select, SameStem))
      unlabelled unmarkStemKey (LikenessRequested(Deselect, SameStem))
      unlabelled classicMarkStemKey (LikenessRequested(Select, SameStem))
      unlabelled classicUnmarkStemKey (LikenessRequested(Deselect, SameStem))
      unlabelled markFilesKey (AllFilesRequested Select)
      unlabelled unmarkFilesKey (AllFilesRequested Deselect)
      unlabelled classicMarkFilesKey (AllFilesRequested Select)
      unlabelled classicUnmarkFilesKey (AllFilesRequested Deselect)
      unlabelled invertKey InversionRequested
      unlabelled starInvertKey InversionRequested
      unlabelled classicInvertKey InversionRequested
      unlabelled recallMarksKey MarksRecalled
      unlabelled classicRecallMarksKey MarksRecalled
      unlabelled insertKey (SelectionToggled Downward)
      unlabelled spaceKey (SelectionToggled Downward)
      unlabelled selectionDownKey (SelectionToggled Downward)
      unlabelled selectionUpKey (SelectionToggled Upward)
      unlabelled keepFavoriteKey FavoriteKeepRequested
      unlabelled copyNamesKey NamesCopyRequested
      unlabelled spareCopyNamesKey NamesCopyRequested
      unlabelled copyPathsKey PathsCopyRequested
      unlabelled spareCopyPathsKey PathsCopyRequested
      unlabelled copyLinkTargetsKey LinkTargetsCopyRequested
      unlabelled spareCopyLinkTargetsKey LinkTargetsCopyRequested
      unlabelled clipboardPathKey ClipboardPathRequested
      unlabelled spareClipboardPathKey ClipboardPathRequested ]
    @ (FavoriteSlot.all
       |> List.map (fun slot -> unlabelled (favoriteKey slot) (FavoriteRequested slot)))

type BarSlot =
    { Name: string
      Key: Key
      Label: string
      Message: Message option }

let private labelledCommand key =
    bindings
    |> List.tryPick (fun binding ->
        match binding.Label with
        | Some label when binding.Key = key -> Some(label, binding.Message)
        | _ -> None)

let bar modifiers =
    functionKeys
    |> List.mapi (fun index functionKey ->
        let key = Modifiers.apply modifiers functionKey
        let command = labelledCommand key

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
