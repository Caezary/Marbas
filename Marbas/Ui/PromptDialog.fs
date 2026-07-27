module Marbas.Ui.PromptDialog

open Terminal.Gui.ViewBase
open Terminal.Gui.Views

let create title label (defaultText: string) =
    let dialog = new Dialog(Title = title, Width = 50, Height = 8)

    dialog.Add(
        new Label(X = 1, Y = 0, Text = label),
        new TextField(X = 1, Y = 1, Width = Dim.Fill 1, Height = 1, Text = defaultText)
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

let text (dialog: Dialog) =
    if dialog.Canceled then None else Some (field dialog).Text
