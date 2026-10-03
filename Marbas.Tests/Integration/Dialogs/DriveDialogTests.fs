module Marbas.Tests.Integration.Dialogs.DriveDialogTests

open System
open System.Drawing
open Xunit
open Terminal.Gui.Views
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Keys
open Marbas.Ui.Theme
open Marbas.Ui.Dialogs

let private fp = FileSystemPath.create

let private system =
    MountedDrive
        { Root = fp @"C:\"
          Label = Some "System"
          Kind = Fixed
          Size = 1000204886016L
          Free = 128849018880L }

let private data =
    MountedDrive
        { Root = fp @"D:\"
          Label = Some "Data"
          Kind = Fixed
          Size = 2000398934016L
          Free = 1319413953331L }

let private rows = [ system; data ]

let private laidOutOn side screenWidth =
    DialogStyle.apply ()
    let dialog = DriveDialog.create side (fun () -> screenWidth) rows
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(screenWidth, 30)) |> ignore
    (DriveDialog.drives dialog).SetFocus() |> ignore
    dialog

let private laidOut () = laidOutOn Left 120

let private shown (dialog: Dialog) =
    let list = DriveDialog.drives dialog
    let source = list.Source.ToList()
    [ for index in 0 .. list.Source.Count - 1 -> source[index].ToString() ]

[<Fact>]
let ``every drive is listed as its line, in the order it was given`` () =
    use dialog = laidOut ()

    Assert.Equal<string list>(DriveLine.describe rows, shown dialog)

[<Fact>]
let ``the list opens on the first drive`` () =
    use dialog = laidOut ()

    Assert.Equal(0, (DriveDialog.drives dialog).SelectedItem.GetValueOrDefault -1)

[<Fact>]
let ``a dialog that was closed without a choice picks nothing`` () =
    use dialog = laidOut ()

    Assert.Equal(None, DriveDialog.choice dialog)

[<Fact>]
let ``what was accepted is the drive the cursor stood on, to stand on`` () =
    use dialog = laidOut ()
    (DriveDialog.drives dialog).SelectedItem <- 1

    dialog.Result <- 1

    Assert.Equal(Some(StandOn data), DriveDialog.choice dialog)

[<Fact>]
let ``alt+R hands over the drive the cursor stood on`` () =
    use dialog = laidOut ()
    (DriveDialog.drives dialog).SelectedItem <- 1

    dialog.NewKeyDownEvent KeyMap.openWithKey |> ignore

    Assert.Equal(Some(HandOver data), DriveDialog.choice dialog)

[<Fact>]
let ``shift+Enter does not hand the drive over`` () =
    use dialog = laidOut ()

    dialog.NewKeyDownEvent KeyMap.classicOpenWithKey |> ignore

    Assert.NotEqual(Some(HandOver system), DriveDialog.choice dialog)

[<Fact>]
let ``the widest line fits inside the frame`` () =
    use dialog = laidOut ()

    let list = (DriveDialog.drives dialog).Frame

    let widest = DriveLine.describe rows |> List.map String.length |> List.max
    Assert.True(list.Width >= widest, $"the drive list at %A{list} cuts its lines")
    Assert.True(list.Right <= dialog.Viewport.Width, $"the drive list at %A{list} runs past the frame")
    Assert.True(list.Bottom <= dialog.Viewport.Height, $"the drive list at %A{list} runs past the frame")

[<Fact>]
let ``an accepted dialog with no drive under the cursor picks nothing`` () =
    use dialog = laidOut ()
    (DriveDialog.drives dialog).SelectedItem <- Nullable()

    dialog.Result <- 1

    let chosen = DriveDialog.choice dialog

    Assert.Equal(None, chosen)

[<Fact>]
let ``an accepted dialog listing no drive picks nothing`` () =
    DialogStyle.apply ()
    use dialog = DriveDialog.create Left (fun () -> 120) []

    dialog.Result <- 1

    let chosen = DriveDialog.choice dialog

    Assert.Equal(None, chosen)

[<Theory>]
[<InlineData(80, 47, 2)>]
[<InlineData(200, 47, 2)>]
[<InlineData(80, 78, 2)>]
[<InlineData(80, 79, 1)>]
[<InlineData(40, 47, 0)>]
let ``the popup of the left panel starts beside the left edge and backs off only to stay on the screen``
    (screenWidth: int, popupWidth: int, expected: int)
    =
    let column = DriveDialog.column Left screenWidth popupWidth

    Assert.Equal(expected, column)

[<Theory>]
[<InlineData(80, 47, 31)>]
[<InlineData(200, 47, 151)>]
[<InlineData(80, 78, 0)>]
[<InlineData(80, 79, 0)>]
[<InlineData(40, 47, 0)>]
let ``the popup of the right panel ends beside the right edge and backs off only to stay on the screen``
    (screenWidth: int, popupWidth: int, expected: int)
    =
    let column = DriveDialog.column Right screenWidth popupWidth

    Assert.Equal(expected, column)

[<Theory>]
[<InlineData(80)>]
[<InlineData(200)>]
let ``the popup of the left panel is laid out over the left panel, beside its outer edge`` (screenWidth: int) =
    use dialog = laidOutOn Left screenWidth

    let frame = dialog.Frame

    Assert.Equal(2, frame.X)

[<Theory>]
[<InlineData(80, 78)>]
[<InlineData(200, 198)>]
let ``the popup of the right panel is laid out over the right panel, beside its outer edge``
    (screenWidth: int, expectedRight: int)
    =
    use dialog = laidOutOn Right screenWidth

    let frame = dialog.Frame

    Assert.Equal(expectedRight, frame.Right)
