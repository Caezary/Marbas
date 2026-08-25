module Marbas.Ui.App.StatusLine

open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.FileSystem.DirectoryListing
open Marbas.Ui.Format

[<Literal>]
let Id = "statusLine"

let create () =
    new Label(Id = Id, Y = Pos.AnchorEnd 2, Width = Dim.Fill(), Height = 1)
    |> LiteralText.showing

let render (entry: Entry option) (line: Label) = line.Text <- CursorInfo.describe entry
