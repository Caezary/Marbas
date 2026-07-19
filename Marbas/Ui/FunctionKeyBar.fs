module Marbas.Ui.FunctionKeyBar

open System
open Terminal.Gui.Input
open Terminal.Gui.Views

[<Literal>]
let Id = "functionKeyBar"

let create (onQuit: unit -> unit) =
    let shortcut (key: Key) title (action: Action) = new Shortcut(key, title, action, null)

    let shortcuts =
        [ shortcut Key.F5 "Copy" null
          shortcut Key.F6 "RenMov" null
          shortcut Key.F7 "Mkdir" null
          shortcut Key.F8 "Delete" null
          shortcut Key.F10 "Quit" (Action onQuit) ]

    new StatusBar(shortcuts, Id = Id)
