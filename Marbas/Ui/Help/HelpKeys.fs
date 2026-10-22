module Marbas.Ui.Help.HelpKeys

open Terminal.Gui.Input
open Marbas.Ui.Keys

type Command =
    | LineUp
    | LineDown
    | PageUp
    | PageDown
    | ToStart
    | ToEnd
    | NextLink
    | PreviousLink
    | Follow
    | Back
    | Closed

let closeKeys = [ Key.Esc; Key.F10 ]

let private bindings =
    [ Key.CursorUp, LineUp
      Key.CursorDown, LineDown
      Key.PageUp, PageUp
      Key.PageDown, PageDown
      Key.Home, ToStart
      Key.End, ToEnd
      Key.Tab, NextLink
      Key.Tab.WithShift, PreviousLink
      Key.Enter, Follow
      Key.Backspace, Back
      Key.CursorLeft.WithAlt, Back ]
    @ (closeKeys |> List.map (fun key -> key, Closed))

let keys = bindings |> List.map fst

let commandFor (key: Key) =
    bindings
    |> List.tryPick (fun (bound, command) -> if bound = key then Some command else None)

let private label (key: Key) = if key = Key.F10 then "Quit" else ""

let bar = KeyMap.functionKeys |> List.map label
