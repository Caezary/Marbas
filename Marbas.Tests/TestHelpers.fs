module Marbas.Tests.TestHelpers

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Terminal.Gui.Input
open Terminal.Gui.ViewBase
open Terminal.Gui.Views
open Marbas.Ui

let panel id (window: Window) =
    window.SubViews |> Seq.find (fun v -> v.Id = id)

let leftList window = FilePanel.listView (panel FilePanel.LeftId window)

let rightList window = FilePanel.listView (panel FilePanel.RightId window)

let press (view: View) (key: Key) =
    view.NewKeyDownEvent(Key key.KeyCode) |> ignore

let dependencies (fs: IFileSystem) startPath : Shell.Dependencies =
    { FileSystem = fs
      StartPath = startPath
      OnQuit = ignore
      ReportError = fun _ _ -> ()
      ChooseOption = fun _ _ -> None
      AskPattern = fun _ -> None
      AskName = fun _ -> None
      AskTarget = fun _ _ -> None
      Confirm = fun _ _ -> false
      AskOverwrite = (fun _ _ -> Marbas.FileSystem.Operations.No)
      RunBatch = fun _ work finish -> work Marbas.FileSystem.Operations.unattended |> finish }

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

let focusedWindow deps =
    let window = MainWindow.create deps
    window.BeginInit()
    window.EndInit()
    window.SetFocus() |> ignore
    window
