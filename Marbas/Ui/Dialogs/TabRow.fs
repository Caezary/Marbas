module Marbas.Ui.Dialogs.TabRow

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.State.AppState

[<Literal>]
let NumberWidth = 4

[<Literal>]
let PathWidth = 32

let describe (fs: IFileSystem) (number: int) (tab: Tab) =
    let where (panel: PanelState) = PanelLocation.value fs panel.Path
    let numbered = $"{number}.".PadRight(NumberWidth - 1)
    let left = (where tab.Left).PadRight(PathWidth - 1)

    $"{numbered} {left} {where tab.Right}"
