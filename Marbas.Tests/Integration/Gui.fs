module Marbas.Tests.Integration.Gui

open System.IO.Abstractions.TestingHelpers
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui
open Marbas.Tests

let panel id (window: Window) =
    window.SubViews |> Seq.find (fun v -> v.Id = id)

let leftList window = FilePanel.listView (panel FilePanel.LeftId window)

let rightList window = FilePanel.listView (panel FilePanel.RightId window)

let press (view: View) (key: Key) =
    view.NewKeyDownEvent(Key key.KeyCode) |> ignore

let focusedWindow deps =
    let window = MainWindow.create deps
    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    window

let createWindowWith onQuit =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"

    MainWindow.create
        { Fakes.dependencies fs @"C:\start" with
            OnQuit = onQuit }

let createWindow () = createWindowWith ignore

let createInitializedWith onQuit =
    let window = createWindowWith onQuit
    window.BeginInit()
    window.EndInit()
    window

let createInitialized () = createInitializedWith ignore
