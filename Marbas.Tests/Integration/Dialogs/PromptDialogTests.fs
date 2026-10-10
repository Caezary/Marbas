module Marbas.Tests.Integration.Dialogs.PromptDialogTests

open System.Drawing
open Xunit
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs

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

let private offering remembered suggestion =
    DialogStyle.apply ()
    let dialog = PromptDialog.offering remembered Shell.MovePrompt "To:" suggestion
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private offered (dialog: Dialog) =
    let field = PromptDialog.field dialog :?> DropDownList
    [ for item in field.Source.ToList() -> string item ]

[<Fact>]
let ``a field with a history offers it as its list, newest first`` () =
    use dialog = offering [ @"D:\newer"; @"D:\older" ] @"C:\backup"

    let listed = offered dialog

    Assert.Equal<string list>([ @"D:\newer"; @"D:\older" ], listed)

[<Fact>]
let ``a field with a history still opens on its suggestion`` () =
    use dialog = offering [ @"D:\newer" ] @"C:\backup"

    let input = PromptDialog.field dialog

    Assert.Equal(@"C:\backup", input.Text)
    Assert.Equal(9, input.InsertionPoint)

[<Fact>]
let ``a field with a history fits inside the dialog content area`` () =
    use dialog = offering [ @"D:\newer" ] ""

    let input = (PromptDialog.field dialog).Frame

    Assert.True(input.Width > 0, "the field must be wide enough to show what is typed")
    Assert.True(input.Bottom <= dialog.Viewport.Height, $"the field at %A{input} is clipped by %A{dialog.Viewport}")

[<Fact>]
let ``a space typed into a field with a history is a space`` () =
    use dialog = offering [ @"D:\newer" ] "Program"
    let input = PromptDialog.field dialog

    input.NewKeyDownEvent Key.Space |> ignore

    Assert.Equal("Program ", input.Text)

[<Theory>]
[<InlineData("F4")>]
[<InlineData("Alt+CursorDown")>]
let ``F4 and Alt+↓ open the list of a field with a history`` (spelled: string) =
    use dialog = offering [ @"D:\newer" ] ""
    let input = PromptDialog.field dialog

    let bound = input.KeyBindings.TryGet(Key.TryParse(spelled) |> snd) |> snd

    Assert.Contains(Command.Toggle, bound.Commands)

[<Fact>]
let ``a confirmed field with a history yields what was typed`` () =
    use dialog = offering [ @"D:\newer" ] @"C:\backup"

    (PromptDialog.field dialog).Text <- @"E:\elsewhere"
    dialog.Result <- 1

    Assert.Equal(Some @"E:\elsewhere", PromptDialog.text dialog)

[<Fact>]
let ``a secret is typed into a plain field, never one with a history`` () =
    use dialog = laidOut "" |> PromptDialog.secret

    let input = PromptDialog.field dialog

    Assert.IsNotType<DropDownList>(input)
