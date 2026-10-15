module Marbas.Ui.Theme.DialogStyle

open System.Text
open Terminal.Gui.Configuration
open Terminal.Gui.Drawing
open Terminal.Gui.ViewBase
open Terminal.Gui.Views

let Frame = LineStyle.Double

let ButtonAlignment = Alignment.Center

let LeftBracket = Rune '['

let RightBracket = Rune ']'

let Checked = Rune '■'

let Unchecked = Rune '·'

let button title =
    let button = new Button(Title = title)
    button.ShadowStyle <- ShadowStyles.None
    button

let typedInto (field: #View) =
    field.SetScheme ClassicScheme.input |> ignore
    field

let dress (dialog: Dialog) =
    dialog.BorderStyle <- Frame
    dialog.ButtonAlignment <- ButtonAlignment
    dialog.ShadowStyle <- ShadowStyles.None
    dialog.SetScheme ClassicScheme.dialog |> ignore
    dialog

let private addScheme schemes scheme =
    match SchemeManager.SchemesToSchemeName schemes with
    | null -> ()
    | name -> SchemeManager.AddScheme(name, scheme)

let apply () =
    addScheme Schemes.Dialog ClassicScheme.dialog
    addScheme Schemes.Error ClassicScheme.errorDialog
    addScheme Schemes.Menu ClassicScheme.input

    Dialog.DefaultBorderStyle <- Frame
    Dialog.DefaultButtonAlignment <- ButtonAlignment
    Dialog.DefaultShadow <- ShadowStyles.None
    MessageBox.DefaultBorderStyle <- Frame
    MessageBox.DefaultButtonAlignment <- ButtonAlignment
    Button.DefaultShadow <- ShadowStyles.None

    Glyphs.LeftBracket <- LeftBracket
    Glyphs.RightBracket <- RightBracket
    Glyphs.CheckStateChecked <- Checked
    Glyphs.CheckStateUnChecked <- Unchecked
