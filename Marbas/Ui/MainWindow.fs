module Marbas.Ui.MainWindow

open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views

let quitKey = Key.Q.WithCtrl

let configureQuitKey () =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All quitKey)

let create () =
    new Window(Title = "Marbas", Width = Dim.Fill(), Height = Dim.Fill())
