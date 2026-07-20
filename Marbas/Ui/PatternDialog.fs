module Marbas.Ui.PatternDialog

open Terminal.Gui.ViewBase
open Terminal.Gui.Views

[<Literal>]
let DefaultPattern = "*"

let create title =
    let dialog = new Dialog(Title = title, Width = 50, Height = 8)

    dialog.Add(
        new Label(X = 1, Y = 0, Text = "Pattern:"),
        new TextField(X = 1, Y = 1, Width = Dim.Fill 1, Height = 1, Text = DefaultPattern)
    )
    |> ignore

    dialog.AddButton(new Button(Title = "_Cancel"))
    dialog.AddButton(new Button(Title = "_Ok"))
    dialog

let field (dialog: Dialog) =
    dialog.SubViews
    |> Seq.pick (function
        | :? TextField as input -> Some input
        | _ -> None)

let pattern (dialog: Dialog) =
    if dialog.Canceled then None else Some (field dialog).Text
