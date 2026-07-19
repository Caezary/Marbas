module Marbas.Tests.MainWindowTests

open Xunit
open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Marbas.Ui

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
let ``quit command is bound to Ctrl+Q`` () =
    MainWindow.configureQuitKey ()
    Assert.Equal(Key.Q.WithCtrl, Application.GetDefaultKey Command.Quit)
