module Marbas.Tests.TestHelpers

open Terminal.Gui.Views
open Marbas.Ui

let panel id (window: Window) =
    window.SubViews |> Seq.find (fun v -> v.Id = id)

let createInitialized () =
    let window = MainWindow.create ()
    window.BeginInit()
    window.EndInit()
    window
