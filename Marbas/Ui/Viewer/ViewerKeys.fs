module Marbas.Ui.Viewer.ViewerKeys

open Terminal.Gui.Input
open Marbas.Ui.Keys

type Command =
    | LineUp
    | LineDown
    | PageUp
    | PageDown
    | ColumnLeft
    | ColumnRight
    | PageLeft
    | PageRight
    | ToStart
    | ToEnd
    | ModeToggled
    | WrapToggled
    | SearchAsked
    | SearchRepeated
    | CodePageAsked
    | HelpAsked
    | Closed

let wrapKey = Key.F2

let modeKey = Key.F4

let searchKey = Key.F7

let repeatKey = Key.F7.WithShift

let codePageKey = Key.F8

let closeKeys = [ Key.F3; Key.F10; Key.Esc ]

let private bindings =
    [ Key.CursorUp, LineUp
      Key.CursorDown, LineDown
      Key.PageUp, PageUp
      Key.PageDown, PageDown
      Key.CursorLeft, ColumnLeft
      Key.CursorRight, ColumnRight
      Key.CursorLeft.WithCtrl, PageLeft
      Key.CursorRight.WithCtrl, PageRight
      Key.Home, ToStart
      Key.Home.WithCtrl, ToStart
      Key.End, ToEnd
      Key.End.WithCtrl, ToEnd
      wrapKey, WrapToggled
      modeKey, ModeToggled
      searchKey, SearchAsked
      repeatKey, SearchRepeated
      codePageKey, CodePageAsked
      KeyMap.helpKey, HelpAsked ]

let keys = (bindings |> List.map fst) @ closeKeys

let commandFor (key: Key) =
    if List.contains key closeKeys then
        Some Closed
    else
        bindings
        |> List.tryPick (fun (bound, command) -> if bound = key then Some command else None)

let private label mode wrap key =
    if key = wrapKey then
        match wrap with
        | Wrapped -> "Unwrap"
        | Unwrapped -> "Wrap"
    elif key = modeKey then
        match mode with
        | TextMode -> "Hex"
        | HexMode -> "Text"
    elif key = searchKey then
        "Search"
    elif key = codePageKey then
        "Code"
    elif key = KeyMap.helpKey then
        "Help"
    elif List.contains key closeKeys then
        "Quit"
    else
        ""

let bar mode wrap =
    KeyMap.functionKeys |> List.map (label mode wrap)
