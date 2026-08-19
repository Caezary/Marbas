module Marbas.Ui.PanelWiring

open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.State.AppState
open Marbas.State.Update

type Host =
    { Dispatch: Message -> unit
      Refresh: unit -> unit
      IsRendering: unit -> bool
      IsSearching: PanelSide -> bool }

let private count (list: ListView) =
    if list.Source = null then 0 else list.Source.Count

let private cursor (list: ListView) = list.SelectedItem.GetValueOrDefault 0

let private page (list: ListView) =
    { PageJump.FirstVisible = list.Viewport.Y
      PageJump.Height = list.Viewport.Height
      PageJump.Count = count list }

let private printable (key: Key) =
    not key.IsCtrl && not key.IsAlt && key.AsRune.Value >= 32 && key.AsRune.Value <> 127

let private searchMessage (key: Key) =
    if key = Key.Esc then Some SearchCancelled
    elif key = Key.Backspace then Some SearchBackspaced
    elif printable key then Some(SearchTyped(char key.AsRune.Value))
    else None

let private stopsAtEdge (list: ListView) (key: Key) =
    if key = Key.CursorUp || key = Key.PageUp || key = Key.Home then
        cursor list = 0
    elif key = Key.CursorDown || key = Key.PageDown || key = Key.End then
        cursor list >= count list - 1
    else
        false

let private jump (list: ListView) (key: Key) =
    if key = Key.CursorRight then
        Some(PageJump.forward (page list) (cursor list))
    elif key = Key.CursorLeft then
        Some(PageJump.backward (page list) (cursor list))
    else
        None

let private command (host: Host) side (list: ListView) (key: Key) =
    match KeyMap.messageFor key with
    | Some message ->
        host.Dispatch message
        true
    | None ->
        match jump list key with
        | Some index when count list > 0 ->
            host.Dispatch(CursorMoved { Side = side; Index = index })
            true
        | Some _ -> true
        | None ->
            if stopsAtEdge list key then
                true
            elif printable key then
                host.Dispatch(SearchTyped(char key.AsRune.Value))
                true
            else
                false

let private handle (host: Host) side (list: ListView) (key: Key) =
    if host.IsSearching side then
        match searchMessage key with
        | Some message ->
            host.Dispatch message
            true
        | None ->
            host.Dispatch SearchCancelled
            command host side list key
    else
        command host side list key

let attach (host: Host) (side: PanelSide) (view: View) =
    let list = FilePanel.listView view
    let cursorAt index = { Side = side; Index = index }

    list.FrameChanged.Add(fun _ -> host.Refresh())

    list.ValueChanged.Add(fun args ->
        if not (host.IsRendering()) then
            host.Dispatch(CursorMoved(cursorAt (args.NewValue.GetValueOrDefault 0))))

    view.HasFocusChanged.Add(fun _ ->
        if view.HasFocus then
            host.Dispatch(PanelFocused side))

    list.Accepting.Add(fun args ->
        args.Handled <- true
        host.Dispatch EntryOpened)

    list.KeyDown.Add(fun key -> key.Handled <- handle host side list key)
