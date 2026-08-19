module Marbas.Ui.ClassicScheme

open Terminal.Gui.Drawing

let private attribute (foreground: ColorName16) (background: ColorName16) =
    let mutable foreground = foreground
    let mutable background = background
    Attribute(&foreground, &background)

let screen =
    Scheme(
        Normal = attribute ColorName16.Gray ColorName16.Black,
        HotNormal = attribute ColorName16.White ColorName16.Black,
        Focus = attribute ColorName16.Black ColorName16.Gray,
        HotFocus = attribute ColorName16.White ColorName16.Gray,
        Disabled = attribute ColorName16.DarkGray ColorName16.Black
    )

let panel =
    Scheme(
        Normal = attribute ColorName16.BrightCyan ColorName16.Blue,
        HotNormal = attribute ColorName16.BrightYellow ColorName16.Blue,
        Focus = attribute ColorName16.Black ColorName16.Cyan,
        HotFocus = attribute ColorName16.BrightYellow ColorName16.Cyan,
        Highlight = attribute ColorName16.BrightYellow ColorName16.Blue,
        Disabled = attribute ColorName16.DarkGray ColorName16.Blue
    )

let directoryEntry = attribute ColorName16.White ColorName16.Blue

let plainEntry = attribute ColorName16.BrightBlue ColorName16.Blue

let archiveEntry = attribute ColorName16.BrightMagenta ColorName16.Blue

let executableEntry = attribute ColorName16.BrightGreen ColorName16.Blue

let summary =
    Scheme(
        Normal = attribute ColorName16.BrightYellow ColorName16.Blue,
        HotNormal = attribute ColorName16.White ColorName16.Blue
    )

let keyBar =
    Scheme(
        Normal = attribute ColorName16.Black ColorName16.Cyan,
        HotNormal = attribute ColorName16.White ColorName16.Cyan,
        Focus = attribute ColorName16.Black ColorName16.Cyan,
        HotFocus = attribute ColorName16.White ColorName16.Cyan
    )
