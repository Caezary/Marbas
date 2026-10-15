module Marbas.Tests.Integration.Viewer.CodePageDialogTests

open System
open System.Drawing
open Xunit
open Terminal.Gui.Views
open Marbas.FileSystem
open Marbas.Ui.Theme
open Marbas.Ui.Viewer
open Marbas.Tests

let private utf8 = { Charset = Utf8; Bom = WithoutBom }

let private laidOutOn current =
    DialogStyle.apply ()
    let dialog = CodePageDialog.create { Detected = utf8; Current = current }
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private rows (dialog: Dialog) =
    let list = CodePageDialog.pages dialog
    [ for item in (Expect.notNull list.Source).ToList() -> string item ]

let private cursor (dialog: Dialog) =
    (CodePageDialog.pages dialog).SelectedItem.GetValueOrDefault -1

[<Fact>]
let ``auto and every code page are listed`` () =
    use dialog = laidOutOn Automatic

    let listed = rows dialog

    Assert.Equal<string list>(CodePage.offered |> List.map (CodePage.label utf8), listed)

[<Fact>]
let ``a viewer that settled on its own opens the list on auto`` () =
    use dialog = laidOutOn Automatic

    let under = cursor dialog

    Assert.Equal(0, under)

[<Fact>]
let ``the list opens on the code page in use`` () =
    use dialog = laidOutOn (Fixed Cp852)

    let under = cursor dialog

    Assert.Equal(6, under)

[<Fact>]
let ``a dialog that was closed without a choice picks nothing`` () =
    use dialog = laidOutOn Automatic

    let chosen = CodePageDialog.choice dialog

    Assert.Equal(None, chosen)

[<Fact>]
let ``what was accepted is the code page the cursor stood on`` () =
    use dialog = laidOutOn Automatic
    (CodePageDialog.pages dialog).SelectedItem <- 7
    dialog.Result <- 1

    let chosen = CodePageDialog.choice dialog

    Assert.Equal(Some(Fixed Iso88592), chosen)

[<Fact>]
let ``an accepted dialog with nothing under the cursor picks nothing`` () =
    use dialog = laidOutOn Automatic
    (CodePageDialog.pages dialog).SelectedItem <- Nullable()
    dialog.Result <- 1

    let chosen = CodePageDialog.choice dialog

    Assert.Equal(None, chosen)

[<Fact>]
let ``the list stays inside the frame`` () =
    use dialog = laidOutOn Automatic

    let list = (CodePageDialog.pages dialog).Frame

    Assert.True(list.Right <= dialog.Viewport.Width, $"the list at %A{list} runs past the frame")
    Assert.True(list.Bottom <= dialog.Viewport.Height, $"the list at %A{list} runs past the frame")

[<Fact>]
let ``every code page fits without scrolling`` () =
    use dialog = laidOutOn Automatic

    let height = (CodePageDialog.pages dialog).Frame.Height

    Assert.Equal(List.length CodePage.offered, height)
