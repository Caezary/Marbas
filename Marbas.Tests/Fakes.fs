module Marbas.Tests.Fakes

open System.IO.Abstractions
open Marbas.FileSystem
open Marbas.Ui

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
      AskOverwrite = (fun _ _ -> Operations.No)
      RunBatch = fun _ work finish -> work Operations.unattended |> finish }
