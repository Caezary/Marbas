module Marbas.Tests.Integration.Dialogs.DriveDialogTests

open System.Drawing
open Xunit
open Terminal.Gui.Views
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs

let private lines =
    [ "C:\\  System  fixed  931.5 GB  120.0 GB free"
      "D:\\  Data    fixed    1.8 TB    1.2 TB free" ]

let private laidOut () =
    DialogStyle.apply ()
    let dialog = DriveDialog.create lines
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(120, 30)) |> ignore
    dialog

let private rows (dialog: Dialog) =
    let list = DriveDialog.drives dialog
    let source = list.Source.ToList()
    [ for index in 0 .. list.Source.Count - 1 -> source[index].ToString() ]

[<Fact>]
let ``every drive is listed in the order it was given`` () =
    use dialog = laidOut ()

    Assert.Equal<string list>(lines, rows dialog)

[<Fact>]
let ``the list opens on the first drive`` () =
    use dialog = laidOut ()

    Assert.Equal(0, (DriveDialog.drives dialog).SelectedItem.GetValueOrDefault -1)

[<Fact>]
let ``a dialog that was closed without a choice picks nothing`` () =
    use dialog = laidOut ()

    Assert.Equal(None, DriveDialog.choice dialog)

[<Fact>]
let ``what was accepted is the drive the cursor stood on`` () =
    use dialog = laidOut ()
    (DriveDialog.drives dialog).SelectedItem <- 1

    dialog.Result <- 1

    Assert.Equal(Some 1, DriveDialog.choice dialog)

[<Fact>]
let ``the widest line fits inside the frame`` () =
    use dialog = laidOut ()

    let list = (DriveDialog.drives dialog).Frame

    Assert.True(list.Width >= lines.Head.Length, $"the drive list at %A{list} cuts its lines")
    Assert.True(list.Right <= dialog.Viewport.Width, $"the drive list at %A{list} runs past the frame")
    Assert.True(list.Bottom <= dialog.Viewport.Height, $"the drive list at %A{list} runs past the frame")
