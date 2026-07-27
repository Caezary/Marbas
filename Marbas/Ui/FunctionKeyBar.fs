module Marbas.Ui.FunctionKeyBar

open System
open Terminal.Gui.Input
open Terminal.Gui.Views

[<Literal>]
let Id = "functionKeyBar"

type Actions =
    { OnCopy: unit -> unit
      OnRenameMove: unit -> unit
      OnMakeDirectory: unit -> unit
      OnDelete: unit -> unit
      OnQuit: unit -> unit }

let create (actions: Actions) =
    let shortcut (key: Key) title (action: unit -> unit) = new Shortcut(key, title, Action action, null)

    let shortcuts =
        [ shortcut Key.F5 "Copy" actions.OnCopy
          shortcut Key.F6 "RenMov" actions.OnRenameMove
          shortcut Key.F7 "Mkdir" actions.OnMakeDirectory
          shortcut Key.F8 "Delete" actions.OnDelete
          shortcut Key.F10 "Quit" actions.OnQuit ]

    new StatusBar(shortcuts, Id = Id)
