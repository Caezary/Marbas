module Marbas.Ui.FunctionKeyBar

open System
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.State.Update

[<Literal>]
let Id = "functionKeyBar"

let bindings =
    [ Key.F5, "Copy", CopyRequested
      Key.F6, "RenMov", MoveRequested
      Key.F7, "Mkdir", MakeDirectoryRequested
      Key.F8, "Delete", DeleteRequested
      Key.F10, "Quit", QuitRequested ]

let create (dispatch: Message -> unit) =
    let shortcut (key: Key, title: string, message) =
        new Shortcut(key, title, Action(fun () -> dispatch message), null)

    new StatusBar(bindings |> List.map shortcut, Id = Id)
