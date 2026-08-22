module Marbas.Ui.ViewerKeys

open Terminal.Gui.Input

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
    | Closed

let wrapKey = Key.F2

let modeKey = Key.F4

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
      modeKey, ModeToggled ]

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
    elif List.contains key closeKeys then
        "Quit"
    else
        ""

let bar mode wrap =
    KeyMap.functionKeys |> List.map (label mode wrap)
