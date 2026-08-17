module Marbas.Tests.Integration.Gui

open System.IO.Abstractions.TestingHelpers
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui
open Marbas.Tests

let private at = Root.at

let panel id (window: Window) =
    window.SubViews |> Seq.find (fun v -> v.Id = id)

let panelTitle id window = (panel id window).Title

let shownPath id window =
    (panelTitle id window).Split " [" |> Array.head

let leftList window = FilePanel.listView (panel FilePanel.LeftId window)

let rightList window = FilePanel.listView (panel FilePanel.RightId window)

let press (view: View) (key: Key) =
    view.NewKeyDownEvent(Key key.KeyCode) |> ignore

let private focusedList window =
    if (panel FilePanel.RightId window).HasFocus then
        rightList window
    else
        leftList window

let pressKey window key = press (focusedList window) key

let focusedWindow deps =
    let window = MainWindow.create deps
    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    window

let createWindowWith onQuit =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start")

    MainWindow.create
        { Fakes.dependencies fs (at "start") with
            OnQuit = onQuit }

let createWindow () = createWindowWith ignore

let createInitializedWith onQuit =
    let window = createWindowWith onQuit
    window.BeginInit()
    window.EndInit()
    window

let createInitialized () = createInitializedWith ignore
