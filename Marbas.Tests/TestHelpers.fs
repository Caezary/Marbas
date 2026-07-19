module Marbas.Tests.TestHelpers

open Terminal.Gui.Views
open Marbas.Ui

let panel id (window: Window) =
    window.SubViews |> Seq.find (fun v -> v.Id = id)

let createInitializedWith onQuit =
    let window = MainWindow.create onQuit
    window.BeginInit()
    window.EndInit()
    window

let createInitialized () = createInitializedWith ignore
