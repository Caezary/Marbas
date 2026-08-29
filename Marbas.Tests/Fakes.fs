module Marbas.Tests.Fakes

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.Ui.App
open Marbas.Tests

let private at = Root.at

let dependencies (fs: IFileSystem) (startPath: string) : Shell.Dependencies =
    { FileSystem = fs
      StartPath = FileSystemPath.create startPath
      SettingsPath = FileSystemPath.create (at "marbas.conf")
      Roots = MountedDrives
      OnQuit = ignore
      ReportError = fun _ _ -> ()
      ChooseOption = fun _ _ -> None
      AskPattern = fun _ -> None
      AskName = fun _ _ -> None
      AskTarget = fun _ _ -> None
      Confirm = fun _ _ -> false
      SendToTrash = fun _ -> Ok()
      ShowFile = ignore
      RunEditor = fun _ _ -> Ok()
      EditProperties = fun _ -> None
      EditEditors = fun _ _ -> None
      AskArchive = (fun _ -> None)
      AskPassword = (fun _ _ -> None)
      AskOverwrite = (fun _ _ -> Operations.No)
      WatchModifiers = ignore
      RunBatch = fun _ work finish -> work Operations.unattended |> finish }
