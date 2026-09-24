module Marbas.Tests.Integration.Dialogs.PromptDialogTests

open System.Drawing
open Xunit
open Marbas.Domain
open Marbas.FileSystem
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

[<Fact>]
let ``a confirmed name comes back as an entry name`` () =
    use dialog = laidOut "work"

    (PromptDialog.field dialog).Text <- "reports"
    dialog.Result <- 1

    Assert.Equal(Some(EntryName.create "reports"), PromptDialog.name dialog)

[<Fact>]
let ``a name is trimmed of the spaces around it`` () =
    use dialog = laidOut ""

    (PromptDialog.field dialog).Text <- "  reports  "
    dialog.Result <- 1

    Assert.Equal(Some(EntryName.create "reports"), PromptDialog.name dialog)

[<Fact>]
let ``a blank name is no name at all`` () =
    use dialog = laidOut ""

    (PromptDialog.field dialog).Text <- "   "
    dialog.Result <- 1

    Assert.Equal(None, PromptDialog.name dialog)

[<Fact>]
let ``a dismissed prompt yields no name`` () =
    use dialog = laidOut "work"

    Assert.Equal(None, PromptDialog.name dialog)

[<Fact>]
let ``a confirmed secret comes back as an archive password`` () =
    use dialog = laidOut "" |> PromptDialog.secret

    (PromptDialog.field dialog).Text <- "hunter2"
    dialog.Result <- 1

    Assert.Equal(ArchivePassword.tryCreate "hunter2", PromptDialog.password dialog)

[<Fact>]
let ``an empty secret is no password at all`` () =
    use dialog = laidOut "" |> PromptDialog.secret

    dialog.Result <- 1

    Assert.Equal(None, PromptDialog.password dialog)
