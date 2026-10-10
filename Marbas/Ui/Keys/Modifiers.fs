namespace Marbas.Ui.Keys

open Terminal.Gui.Input
open Marbas.Domain

module Modifiers =

    let none: Set<Modifier> = Set.empty

    let held modifiers = Set.ofList modifiers

    let ofKey (key: Key) =
        [ if key.IsCtrl then
              Ctrl
          if key.IsAlt then
              Alt
          if key.IsShift then
              Shift ]
        |> Set.ofList

    let apply modifiers (key: Key) =
        modifiers
        |> Set.fold
            (fun (key: Key) modifier ->
                match modifier with
                | Ctrl -> key.WithCtrl
                | Alt -> key.WithAlt
                | Shift -> key.WithShift)
            key
