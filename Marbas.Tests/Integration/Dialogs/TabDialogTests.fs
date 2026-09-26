module Marbas.Tests.Integration.Dialogs.TabDialogTests

open System
open System.Drawing
open Xunit
open Terminal.Gui.Views
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs

let private lines = [ "1. left one"; "2. left two"; "3. left three" ]

let private laidOutOn current =
    DialogStyle.apply ()
    let dialog = TabDialog.create lines current
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(90, 25)) |> ignore
    dialog

let private rows (dialog: Dialog) =
    let list = TabDialog.tabs dialog
    let source = list.Source.ToList()
    [ for index in 0 .. list.Source.Count - 1 -> source[index].ToString() ]

[<Fact>]
let ``every tab is listed in the order it was given`` () =
    use dialog = laidOutOn 0

    Assert.Equal<string list>(lines, rows dialog)

[<Fact>]
let ``the list opens on the tab in front`` () =
    use dialog = laidOutOn 2

    Assert.Equal(2, (TabDialog.tabs dialog).SelectedItem.GetValueOrDefault -1)

[<Fact>]
let ``a dialog that was closed without a choice picks nothing`` () =
    use dialog = laidOutOn 1

    Assert.Equal(None, TabDialog.choice dialog)

[<Fact>]
let ``what was accepted is the tab the cursor stood on`` () =
    use dialog = laidOutOn 0
    (TabDialog.tabs dialog).SelectedItem <- 2

    dialog.Result <- 1

    Assert.Equal(Some 2, TabDialog.choice dialog)

[<Fact>]
let ``the list stays inside the frame`` () =
    use dialog = laidOutOn 0
    let list = (TabDialog.tabs dialog).Frame

    Assert.True(list.Right <= dialog.Viewport.Width, $"the tab list at %A{list} runs past the frame")
    Assert.True(list.Bottom <= dialog.Viewport.Height, $"the tab list at %A{list} runs past the frame")

[<Fact>]
let ``an accepted dialog with no tab under the cursor picks nothing`` () =
    use dialog = laidOutOn 0
    (TabDialog.tabs dialog).SelectedItem <- Nullable()

    dialog.Result <- 1

    let chosen = TabDialog.choice dialog

    Assert.Equal(None, chosen)
