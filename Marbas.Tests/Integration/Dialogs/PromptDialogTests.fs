module Marbas.Tests.Integration.Dialogs.PromptDialogTests

open System.Drawing
open Xunit
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Ui.App

let private laidOut suggestion =
    DialogStyle.apply ()
    let dialog = PromptDialog.create Shell.MkdirPrompt "Name:" suggestion
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

[<Fact>]
let ``the input field fits inside the dialog content area`` () =
    use dialog = laidOut ""

    let input = (PromptDialog.field dialog).Frame

    Assert.True(input.Width > 0, "the field must be wide enough to show what is typed")
    Assert.True(input.Bottom <= dialog.Viewport.Height, $"the field at %A{input} is clipped by %A{dialog.Viewport}")

[<Fact>]
let ``a suggested name is offered ready to be edited`` () =
    use dialog = laidOut "work"

    let input = PromptDialog.field dialog

    Assert.Equal("work", input.Text)
    Assert.Equal(4, input.InsertionPoint)

[<Fact>]
let ``a prompt with nothing to suggest opens empty`` () =
    use dialog = laidOut ""

    let input = PromptDialog.field dialog

    Assert.Equal("", input.Text)
    Assert.Equal(0, input.InsertionPoint)

[<Fact>]
let ``a dismissed prompt yields no text`` () =
    use dialog = laidOut "work"

    Assert.Equal(None, PromptDialog.text dialog)

[<Fact>]
let ``confirming yields the text that was typed`` () =
    use dialog = laidOut "work"

    (PromptDialog.field dialog).Text <- "reports"
    dialog.Result <- 1

    Assert.Equal(Some "reports", PromptDialog.text dialog)

[<Fact>]
let ``a suggestion can be offered selected, so the first key typed replaces it`` () =
    use dialog = laidOut "work" |> PromptDialog.selecting

    let input = PromptDialog.field dialog

    Assert.Equal(0, input.SelectedStart)
    Assert.Equal(4, input.SelectedLength)
    Assert.Equal("work", input.SelectedText)

[<Fact>]
let ``a prompt can hide what is typed into it`` () =
    use dialog = laidOut "" |> PromptDialog.secret

    Assert.True((PromptDialog.field dialog).Secret)
