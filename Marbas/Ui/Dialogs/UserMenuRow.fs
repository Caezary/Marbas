module Marbas.Ui.Dialogs.UserMenuRow

open System
open Marbas.Domain
open Marbas.FileSystem

[<Literal>]
let private HotkeyGap = "  "

[<Literal>]
let SeparatorGlyph = '─'

let describe (width: int) item =
    match item with
    | CommandItem command -> $"{MenuHotkey.value command.Hotkey}{HotkeyGap}{MenuLabel.value command.Label}"
    | SeparatorItem -> String(SeparatorGlyph, max width 0)
