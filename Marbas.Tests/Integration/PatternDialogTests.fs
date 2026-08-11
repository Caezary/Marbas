module Marbas.Tests.Integration.PatternDialogTests

open System.Drawing
open Xunit
open Marbas.Ui

let private laidOut title =
    let dialog = PatternDialog.create title
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

[<Fact>]
let ``the input field fits inside the dialog content area`` () =
    use dialog = laidOut Shell.SelectPrompt

    let input = (PatternDialog.field dialog).Frame

    Assert.True(input.Width > 0, "the field must be wide enough to show what is typed")
    Assert.True(input.Bottom <= dialog.Viewport.Height, $"the field at %A{input} is clipped by %A{dialog.Viewport}")

[<Fact>]
let ``the dialog opens with a pattern matching everything`` () =
    use dialog = laidOut Shell.SelectPrompt

    let typed = (PatternDialog.field dialog).Text

    Assert.Equal(PatternDialog.DefaultPattern, typed)

[<Fact>]
let ``a dismissed dialog yields no pattern`` () =
    use dialog = laidOut Shell.DeselectPrompt

    let pattern = PatternDialog.pattern dialog

    Assert.Equal(None, pattern)

[<Fact>]
let ``confirming yields the text that was typed`` () =
    use dialog = laidOut Shell.SelectPrompt

    (PatternDialog.field dialog).Text <- "*.fs"
    dialog.Result <- 1

    Assert.Equal(Some "*.fs", PatternDialog.pattern dialog)
