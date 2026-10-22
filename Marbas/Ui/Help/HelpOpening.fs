module Marbas.Ui.Help.HelpOpening

open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Marbas.Ui.Keys

let onHelpKey (show: unit -> unit) (view: #View) =
    view.KeyDown.Add(fun (key: Key) ->
        if key = KeyMap.helpKey then
            key.Handled <- true
            show ())

    view
