module Marbas.Ui.MainWindow

open Terminal.Gui.App
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views

let quitKey = Key.Q.WithCtrl

let configureQuitKey () =
    Application.SetDefaultKeyBinding(Command.Quit, Bind.All(quitKey, Key.F10))

let create onQuit =
    let window = new Window(Title = "Marbas", Width = Dim.Fill(), Height = Dim.Fill())
    let left, right = FilePanel.createPair ()
    let bar = FunctionKeyBar.create onQuit
    window.Add(left, right, bar) |> ignore
    window
