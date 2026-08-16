module Marbas.Tests.Fakes

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.Ui

let dependencies (fs: IFileSystem) (startPath: string) : Shell.Dependencies =
    { FileSystem = fs
      StartPath = FileSystemPath.create startPath
      OnQuit = ignore
      ReportError = fun _ _ -> ()
      ChooseOption = fun _ _ -> None
      AskPattern = fun _ -> None
      AskName = fun _ -> None
      AskTarget = fun _ _ -> None
      Confirm = fun _ _ -> false
      ShowFile = fun _ _ -> ()
      RunEditor = fun _ -> Ok()
      AskOverwrite = (fun _ _ -> Operations.No)
      WatchModifiers = ignore
      RunBatch = fun _ work finish -> work Operations.unattended |> finish }
