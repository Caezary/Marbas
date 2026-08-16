namespace Marbas.Ui

open Terminal.Gui.Input

type Modifier =
    | Ctrl
    | Alt
    | Shift

module Modifiers =

    let none: Set<Modifier> = Set.empty

    let held modifiers = Set.ofList modifiers

    let ofKey (key: Key) =
        [ if key.IsCtrl then Ctrl
          if key.IsAlt then Alt
          if key.IsShift then Shift ]
        |> Set.ofList
