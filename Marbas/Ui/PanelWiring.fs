module Marbas.Ui.PanelWiring

open Terminal.Gui.ViewBase
open Marbas.State.AppState
open Marbas.State.Update

type Host =
    { Dispatch: Message -> unit
      Refresh: unit -> unit
      IsRendering: unit -> bool }

let attach (host: Host) (side: PanelSide) (view: View) =
    let list = FilePanel.listView view
    let cursorAt index = { Side = side; Index = index }
    let cursor () = cursorAt (list.SelectedItem.GetValueOrDefault 0)

    list.FrameChanged.Add(fun _ -> host.Refresh())

    list.ValueChanged.Add(fun args ->
        if not (host.IsRendering()) then
            host.Dispatch(CursorMoved(cursorAt (args.NewValue.GetValueOrDefault 0))))

    view.HasFocusChanged.Add(fun _ ->
        if view.HasFocus then
            host.Dispatch(PanelFocused side))

    list.Accepting.Add(fun args ->
        args.Handled <- true
        host.Dispatch(EntryOpened(cursor ())))

    list.KeyDown.Add(fun key ->
        match PanelInput.messageFor (cursor ()) key with
        | Some msg ->
            key.Handled <- true
            host.Dispatch msg
        | None -> ())
