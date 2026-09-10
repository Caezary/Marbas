module Marbas.Adapters.Dependencies

open System
open System.IO.Abstractions
open Terminal.Gui.App
open Marbas
open Marbas.FileSystem
open Marbas.Ui.App

let private sendToTrash (fs: IFileSystem) (env: HostEnvironment) =
    match env.Platform with
    | Windows -> RecycleBin.send fs
    | Unix ->
        let trash = Trash.location fs env.DataHome env.UserProfile

        Trash.send fs trash (fun () -> DateTimeOffset.Now)

let private roots (env: HostEnvironment) =
    match env.Platform with
    | Windows -> MountedDrives
    | Unix -> env.UserProfile |> FileSystemPath.create |> RootAndHome

let private watchModifiers (app: IApplication) (env: HostEnvironment) =
    match env.Platform with
    | Windows -> ModifierWatch.poll app
    | Unix -> ModifierWatch.unsupported

let private handlersFor (fs: IFileSystem) (env: HostEnvironment) =
    match env.Platform with
    | Windows -> RegistryHandlers.read
    | Unix ->
        XdgDirectories.resolve fs env.UserProfile env.DataHome env.DataDirectories env.ConfigHome env.ConfigDirectories
        |> DesktopHandlers.read fs

let private runEditor (app: IApplication) (fs: IFileSystem) (env: HostEnvironment) =
    EditorProcess.run fs (fun () -> app.ClearScreenNextIteration <- true) env.Editor env.Platform

let create (app: IApplication) (fs: IFileSystem) (env: HostEnvironment) : Shell.Dependencies =
    { FileSystem = fs
      StartPath = fs.Directory.GetCurrentDirectory() |> FileSystemPath.create
      SettingsPath = Settings.location fs env.ApplicationData env.UserProfile
      WorkspacePath = Workspace.location fs env.TemporaryRoot env.Session
      Roots = roots env
      OnQuit = fun () -> app.RequestStop()
      ReportError = Prompts.reportError app
      ChooseOption = Prompts.chooseOption app
      AskPattern = Prompts.askPattern app
      AskName = Prompts.askName app
      AskTarget = Prompts.askTarget app
      Confirm = Prompts.confirm app
      SendToTrash = sendToTrash fs env
      ShowFile = Prompts.showFile app
      RunEditor = runEditor app fs env
      Launch = LaunchProcess.start fs env.Platform
      HandlersFor = handlersFor fs env
      AskHandler = Prompts.askHandler app
      ChooseTab = Prompts.chooseTab app fs
      EditProperties = Prompts.editProperties app
      EditPrograms = Prompts.editPrograms app
      AskArchive = Prompts.askArchive app (ArchiveName.retarget fs)
      AskPassword = Prompts.askPassword app
      AskOverwrite = Prompts.askOverwrite app
      WatchModifiers = watchModifiers app env
      WatchLocations = DirectoryWatch.watching app fs
      RunBatch = BatchRunner.run app }
