module Marbas.Ui.Find.FindKeys

open Terminal.Gui.Input
open Marbas.Ui.Keys

let viewKey = Key.F3

let standKey = Key.Enter

let quitKey = Key.F10

let closeKeys = [ quitKey; Key.Esc ]

let private label key =
    if key = viewKey then "View"
    elif key = quitKey then "Quit"
    else ""

let bar = KeyMap.functionKeys |> List.map label

let closes (key: Key) = List.contains key closeKeys
