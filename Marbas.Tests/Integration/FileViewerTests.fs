module Marbas.Tests.Integration.FileViewerTests

open System
open System.Drawing
open Xunit
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.Ui
open Marbas.Tests.Integration.Gui
open Marbas.Tests

let private at = Root.at

let private viewing close lines =
    let dialog = FileViewer.create close (at "start/notes.txt") lines
    dialog.BeginInit()
    dialog.EndInit()
    dialog.Layout(Size(80, 24)) |> ignore
    dialog

let private laidOut lines = viewing ignore lines

[<Fact>]
let ``the content fits inside the dialog content area`` () =
    use dialog = laidOut [ "first"; "second" ]

    let content = (FileViewer.content dialog).Frame

    Assert.True(content.Width > 0, "the content must be wide enough to show a line")
    Assert.True(content.Height > 0, "the content must be tall enough to show a line")

    Assert.True(
        content.Bottom <= dialog.Viewport.Height,
        $"the content at %A{content} is clipped by %A{dialog.Viewport}"
    )

[<Fact>]
let ``the viewer shows every line it was given`` () =
    use dialog = laidOut [ "first"; "second" ]

    let shown = (FileViewer.content dialog).Text

    Assert.Equal(String.concat Environment.NewLine [ "first"; "second" ], shown)

[<Fact>]
let ``the viewer is read only`` () =
    use dialog = laidOut [ "first" ]

    let content = FileViewer.content dialog

    Assert.True(content.ReadOnly)

[<Fact>]
let ``F3 closes the viewer`` () =
    let closed = ResizeArray<Dialog>()
    use dialog = viewing closed.Add [ "first" ]

    press (FileViewer.content dialog) Key.F3

    Assert.Same(dialog, Assert.Single closed)

[<Fact>]
let ``Esc closes the viewer`` () =
    let closed = ResizeArray<Dialog>()
    use dialog = viewing closed.Add [ "first" ]

    press (FileViewer.content dialog) Key.Esc

    Assert.Same(dialog, Assert.Single closed)

[<Fact>]
let ``another key leaves the viewer open`` () =
    let closed = ResizeArray<Dialog>()
    use dialog = viewing closed.Add [ "first" ]

    press (FileViewer.content dialog) Key.CursorDown

    Assert.Empty closed
