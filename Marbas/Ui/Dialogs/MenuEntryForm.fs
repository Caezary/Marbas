module Marbas.Ui.Dialogs.MenuEntryForm

open FsToolkit.ErrorHandling
open Marbas.Domain
open Marbas.FileSystem

type LineSide =
    | AboveCursor
    | BelowCursor

type EntryAnswer =
    | CommandEntered of MenuCommand
    | LineEntered of LineSide

type EntryEditing =
    | Adding
    | Changing of MenuCommand

type EntryPrompt =
    { Editing: EntryEditing
      Others: MenuCommand list }

type EntryFields =
    { LabelText: string
      HotkeyText: string
      LineText: string
      ChosenRun: MenuRun
      ChosenElevation: Elevation }

type EntryRefusal =
    | NoLabel
    | NoHotkey
    | HotkeyHeld of holder: MenuCommand
    | NoCommandLine

let private notHeld (others: MenuCommand list) hotkey =
    match others |> List.tryFind (fun other -> other.Hotkey = hotkey) with
    | Some holder -> Error(HotkeyHeld holder)
    | None -> Ok hotkey

let read (others: MenuCommand list) (fields: EntryFields) =
    result {
        let! label = MenuLabel.tryCreate fields.LabelText |> Result.requireSome NoLabel
        let! typed = MenuHotkey.tryCreate fields.HotkeyText |> Result.requireSome NoHotkey
        let! hotkey = notHeld others typed
        let! line = CommandLine.tryCreate fields.LineText |> Result.requireSome NoCommandLine

        return
            { Hotkey = hotkey
              Label = label
              Line = line
              Run = fields.ChosenRun
              Elevation = fields.ChosenElevation }
    }

let fieldsOf (command: MenuCommand) =
    { LabelText = MenuLabel.value command.Label
      HotkeyText = string (MenuHotkey.value command.Hotkey)
      LineText = CommandLine.value command.Line
      ChosenRun = command.Run
      ChosenElevation = command.Elevation }

let describe refusal =
    match refusal with
    | NoLabel -> "An entry needs a label."
    | NoHotkey -> "The hotkey is one letter or one digit."
    | HotkeyHeld holder ->
        $"'{MenuHotkey.value holder.Hotkey}' already runs '{MenuLabel.value holder.Label}'."
    | NoCommandLine -> "An entry needs a command line."
