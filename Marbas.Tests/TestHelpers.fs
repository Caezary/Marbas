module Marbas.Tests.TestHelpers

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui

let panel id (window: Window) =
    window.SubViews |> Seq.find (fun v -> v.Id = id)

let press (view: View) (key: Key) =
    view.NewKeyDownEvent(Key key.KeyCode) |> ignore

let dependencies (fs: IFileSystem) startPath : MainWindow.Dependencies =
    { FileSystem = fs
      StartPath = startPath
      OnQuit = ignore
      ReportError = ignore
      ChooseDrive = fun _ -> None }

let createWindowWith onQuit =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start"

    MainWindow.create
        { dependencies fs @"C:\start" with
            OnQuit = onQuit }

let createWindow () = createWindowWith ignore

let createInitializedWith onQuit =
    let window = createWindowWith onQuit
    window.BeginInit()
    window.EndInit()
    window

let createInitialized () = createInitializedWith ignore
