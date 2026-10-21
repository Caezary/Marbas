module Marbas.Ui.Find.FindKeys

open Terminal.Gui.Input
open Marbas.Ui.Keys

let panelKey = Key.F2

let viewKey = Key.F3

let standKey = Key.Enter

let quitKey = Key.F10

let closeKeys = [ quitKey; Key.Esc ]

let keys = [ panelKey; viewKey; standKey ] @ closeKeys

let private label key =
    if key = panelKey then "Panel"
    elif key = viewKey then "View"
    elif key = quitKey then "Quit"
    else ""

let bar = KeyMap.functionKeys |> List.map label

let closes (key: Key) = List.contains key closeKeys
