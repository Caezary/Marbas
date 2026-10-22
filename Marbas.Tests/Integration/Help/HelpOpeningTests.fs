module Marbas.Tests.Integration.Help.HelpOpeningTests

open Xunit
open Terminal.Gui.Input
open Marbas.Ui.Dialogs
open Marbas.Ui.Help
open Marbas.Tests.Integration.Gui

let private prompt () =
    let dialog = PromptDialog.create "Create directory" "Name:" "work"
    dialog.BeginInit()
    dialog.EndInit()
    dialog

[<Fact>]
let ``F1 in a popup opens the help`` () =
    let opened = ResizeArray<unit>()
    use dialog = prompt () |> HelpOpening.onHelpKey opened.Add

    let key = pressReturning dialog Key.F1

    Assert.Single opened |> ignore
    Assert.True key.Handled

[<Fact>]
let ``F1 while a field of a popup holds the focus opens the help`` () =
    let opened = ResizeArray<unit>()
    use dialog = prompt () |> HelpOpening.onHelpKey opened.Add
    (PromptDialog.field dialog).SetFocus() |> ignore

    press dialog Key.F1

    Assert.Single opened |> ignore

[<Fact>]
let ``another key of a popup opens no help`` () =
    let opened = ResizeArray<unit>()
    use dialog = prompt () |> HelpOpening.onHelpKey opened.Add

    press dialog Key.F2

    Assert.Empty opened
