module Marbas.Tests.Integration.Dialogs.PatternDialogTests

open System.Drawing
open Xunit
open Terminal.Gui.Views
open Marbas.State
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Tests

let private laidOut remembered title =
    DialogStyle.apply ()
    let dialog = PatternDialog.create remembered title
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

[<Fact>]
let ``the input field fits inside the dialog content area`` () =
    use dialog = laidOut [] Shell.SelectPrompt

    let input = (PatternDialog.field dialog).Frame

    Assert.True(input.Width > 0, "the field must be wide enough to show what is typed")
    Assert.True(input.Bottom <= dialog.Viewport.Height, $"the field at %A{input} is clipped by %A{dialog.Viewport}")

[<Fact>]
let ``the dialog opens with a pattern matching everything`` () =
    use dialog = laidOut [ "*.fs" ] Shell.SelectPrompt

    let typed = (PatternDialog.field dialog).Text

    Assert.Equal(PatternDialog.DefaultPattern, typed)

[<Fact>]
let ``the patterns typed before are offered as the field's list`` () =
    use dialog = laidOut [ "*.fs"; "*.txt" ] Shell.SelectPrompt

    let field = PatternDialog.field dialog :?> DropDownList

    Assert.Equal<string list>(
        [ "*.fs"; "*.txt" ],
        [ for item in (Expect.notNull field.Source).ToList() -> string item ]
    )

[<Fact>]
let ``a dismissed dialog yields no pattern`` () =
    use dialog = laidOut [] Shell.DeselectPrompt

    let pattern = PromptDialog.text dialog

    Assert.Equal(None, pattern)

[<Fact>]
let ``confirming yields the pattern as it was typed`` () =
    use dialog = laidOut [] Shell.SelectPrompt

    (PatternDialog.field dialog).Text <- " *.fs"
    dialog.Result <- 1

    Assert.Equal(Some " *.fs", PromptDialog.text dialog)
