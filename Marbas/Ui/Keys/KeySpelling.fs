module Marbas.Ui.Keys.KeySpelling

open System.Text
open Terminal.Gui.Drivers
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.Guide

let private names =
    [ KeyCode.CursorUp, "↑"
      KeyCode.CursorDown, "↓"
      KeyCode.CursorLeft, "←"
      KeyCode.CursorRight, "→"
      KeyCode.PageUp, "PgUp"
      KeyCode.PageDown, "PgDn"
      KeyCode.Home, "Home"
      KeyCode.End, "End"
      KeyCode.Insert, "Ins"
      KeyCode.Delete, "Del"
      KeyCode.Enter, "Enter"
      KeyCode.Esc, "Esc"
      KeyCode.Backspace, "Backspace"
      KeyCode.Tab, "Tab"
      KeyCode.Space, "Space" ]
    @ (KeyMap.functionKeys
       |> List.mapi (fun index (key: Key) -> key.KeyCode, $"F{index + 1}"))
    |> Map.ofList

let private modifierOrder = [ Ctrl; Alt; Shift ]

let private modifierName modifier =
    match modifier with
    | Ctrl -> "Ctrl"
    | Alt -> "Alt"
    | Shift -> "Shift"

let private bareName (bare: Key) =
    match Map.tryFind bare.KeyCode names with
    | Some name -> name
    | None when Rune.IsValid(int bare.KeyCode) -> Rune(int bare.KeyCode).ToString()
    | None -> bare.ToString()

let nameOf (key: Key) =
    let held = Modifiers.ofKey key

    modifierOrder
    |> List.filter held.Contains
    |> List.map modifierName
    |> fun modifiers -> modifiers @ [ bareName key.NoCtrl.NoAlt.NoShift ]
    |> String.concat "+"
    |> KeyName.create
