module Marbas.Tests.Integration.Dialogs.ProgressDialogTests

open System.Drawing
open Xunit
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs
open Marbas.Ui.App
open Marbas.Tests

let private at = Root.at

let private laidOut title =
    DialogStyle.apply ()
    let dialog = ProgressDialog.create title
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

[<Fact>]
let ``the reported entry fits inside the dialog content area`` () =
    use dialog = laidOut Shell.CopyTitle

    ProgressDialog.report dialog (at "start/notes.txt")
    let line = (ProgressDialog.line dialog).Frame

    Assert.True(line.Width > 0, "the line must be wide enough to show a path")
    Assert.True(line.Bottom <= dialog.Viewport.Height, $"the line at %A{line} is clipped by %A{dialog.Viewport}")

[<Fact>]
let ``the dialog shows what is being worked on`` () =
    use dialog = laidOut Shell.MoveTitle

    ProgressDialog.report dialog (at "start/notes.txt")

    Assert.Equal(at "start/notes.txt", (ProgressDialog.line dialog).Text)

[<Fact>]
let ``the dialog opens empty`` () =
    use dialog = laidOut Shell.CopyTitle

    let line = (ProgressDialog.line dialog).Text

    Assert.Equal("", line)
