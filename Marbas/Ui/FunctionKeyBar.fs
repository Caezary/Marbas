module Marbas.Ui.FunctionKeyBar

open System
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.State.Update

[<Literal>]
let Id = "functionKeyBar"

let private shortcut (dispatch: Message -> unit) (key: Key, title: string, message) =
    let entry = new Shortcut(key, title, Action(fun () -> dispatch message), null)
    entry.BindKeyToApplication <- false
    entry

let show (dispatch: Message -> unit) (bar: StatusBar) modifiers =
    bar.RemoveAll() |> Seq.iter _.Dispose()

    KeyMap.row modifiers
    |> List.iteri (fun index binding -> bar.AddShortcutAt(index, shortcut dispatch binding))

    bar.SetNeedsLayout()
    bar.SetNeedsDraw()

let create (dispatch: Message -> unit) =
    let bar = new StatusBar(Id = Id)
    bar.SetScheme ClassicScheme.keyBar |> ignore
    show dispatch bar Modifiers.none
    bar
