module Marbas.Adapters.Dependencies

open System
open System.IO.Abstractions
open Terminal.Gui.App
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.App

let private sendToTrash (fs: IFileSystem) (env: HostEnvironment) =
    match env.Platform with
    | Windows -> RecycleBin.send fs
    | Unix ->
        let trash = Trash.location fs env.DataHome env.UserProfile

        Trash.send fs trash (fun () -> DateTimeOffset.Now)

let private pathSeparator platform =
    match platform with
    | Windows -> ';'
    | Unix -> ':'

let private home (env: HostEnvironment) =
    match env.Platform with
    | Windows -> None
    | Unix -> env.UserProfile |> FileSystemPath.create |> Some

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

let create (app: IApplication) (fs: IFileSystem) (env: HostEnvironment) : Ports.Dependencies =
    let links = NativeLinks.forPlatform env.Platform
    let settingsPath = Settings.location fs env.ApplicationData env.UserProfile

    { FileSystem = fs
      StartPath = fs.Directory.GetCurrentDirectory() |> FileSystemPath.create
      SettingsPath = settingsPath
      HistoryPath = History.beside fs settingsPath
      WorkspacePath = Workspace.location fs env.TemporaryRoot env.Session
      Clock = fun () -> DateTimeOffset.Now
      OnQuit = fun () -> app.RequestStop()
      Prompts =
        { ReportError = Prompts.reportError app
          Confirm = Prompts.confirm app
          AskOverwrite = Prompts.askOverwrite app
          AskPattern = Prompts.askPattern app
          AskName = Prompts.askName app
          AskTarget = Prompts.askTarget app
          AskPassword = Prompts.askPassword app
          AskArchive = Prompts.askArchive app (ArchiveName.retarget fs)
          AskFind = Prompts.askFind app
          AskLink = Prompts.askLink app
          AskHandler = Prompts.askHandler app
          ChooseDrive = Prompts.chooseDrive app
          ChooseTab = Prompts.chooseTab app fs
          EditProperties = Prompts.editProperties app
          EditPrograms = Prompts.editPrograms app
          ShowFile = Prompts.showFile app }
      Processes =
        { ListDrives = fun () -> Drives.list fs (home env)
          SendToTrash = sendToTrash fs env
          RunEditor = runEditor app fs env
          Launch = LaunchProcess.start fs env.Platform
          LinkKinds = Links.available links
          RefuseLink = Links.refusal env.Platform fs
          MakeLink = Links.create links env.Platform fs
          HandlersFor = handlersFor fs env
          PathDirectories = FindGround.pathDirectories fs (pathSeparator env.Platform) env.SearchPath }
      Watches =
        { Modifiers = watchModifiers app env
          Locations = DirectoryWatch.watching app fs }
      Runners =
        { Batch = BatchRunner.run app
          Measure = BatchRunner.runMeasuring app
          Search = SearchRunner.run app fs } }
