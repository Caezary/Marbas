module Marbas.Tests.MainWindowTests

open System.Drawing
open Xunit
open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Marbas.Ui
open Marbas.Tests.TestHelpers

[<Fact>]
let ``main window has application title`` () =
    use window = MainWindow.create ()
    Assert.Equal("Marbas", window.Title)

[<Fact>]
let ``main window fills the whole screen`` () =
    use window = MainWindow.create ()
    Assert.Equal(Dim.Fill(), window.Width)
    Assert.Equal(Dim.Fill(), window.Height)

[<Fact>]
let ``main window contains left and right panels`` () =
    use window = MainWindow.create ()
    Assert.NotNull(panel FilePanel.LeftId window)
    Assert.NotNull(panel FilePanel.RightId window)

[<Theory>]
[<InlineData(80, 24)>]
[<InlineData(120, 30)>]
let ``panels split the window in half at any size`` (width: int) (height: int) =
    use window = MainWindow.create ()
    window.Layout(Size(width, height)) |> ignore
    let content = window.Viewport
    let left = (panel FilePanel.LeftId window).Frame
    let right = (panel FilePanel.RightId window).Frame
    Assert.Equal(content.Width / 2, left.Width)
    Assert.Equal(content.Width - left.Width, right.Width)
    Assert.Equal(content.Height, left.Height)
    Assert.Equal(content.Height, right.Height)
    Assert.Equal(left.X + left.Width, right.X)

[<Fact>]
let ``quit command is bound to Ctrl+Q`` () =
    MainWindow.configureQuitKey ()
    Assert.Equal(Key.Q.WithCtrl, Application.GetDefaultKey Command.Quit)
