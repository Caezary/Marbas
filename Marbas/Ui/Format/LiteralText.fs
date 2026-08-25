module Marbas.Ui.Format.LiteralText

open System.Text
open Terminal.Gui.ViewBase

[<Literal>]
let private NoHotKey = 0xffff

let showing (view: #View) =
    view.HotKeySpecifier <- Rune NoHotKey
    view
