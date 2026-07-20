module Marbas.Tests.TestHelpers

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Terminal.Gui.Views
open Marbas.Ui

let panel id (window: Window) =
    window.SubViews |> Seq.find (fun v -> v.Id = id)

let createWindowWith onQuit =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"
    MainWindow.create (fs :> IFileSystem) @"C:\start" onQuit ignore

let createWindow () = createWindowWith ignore

let createInitializedWith onQuit =
    let window = createWindowWith onQuit
    window.BeginInit()
    window.EndInit()
    window

let createInitialized () = createInitializedWith ignore
