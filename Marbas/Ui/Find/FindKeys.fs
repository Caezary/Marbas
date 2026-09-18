module Marbas.Ui.Find.FindKeys

open Terminal.Gui.Input
open Marbas.Ui.Keys

let quitKey = Key.F10

let closeKeys = [ quitKey; Key.Esc ]

let private label key = if key = quitKey then "Quit" else ""

let bar = KeyMap.functionKeys |> List.map label

let closes (key: Key) = List.contains key closeKeys
