namespace Marbas.Adapters

open System
open Marbas.Domain

type HostEnvironment =
    { Platform: Platform
      UserProfile: string
      ApplicationData: string
      DataHome: string
      DataDirectories: string
      ConfigHome: string
      ConfigDirectories: string
      TemporaryRoot: string
      Session: string
      SearchPath: string
      Editor: string option }

module HostEnvironment =

    [<Literal>]
    let DataHomeVariable = "XDG_DATA_HOME"

    [<Literal>]
    let DataDirectoriesVariable = "XDG_DATA_DIRS"

    [<Literal>]
    let ConfigHomeVariable = "XDG_CONFIG_HOME"

    [<Literal>]
    let ConfigDirectoriesVariable = "XDG_CONFIG_DIRS"

    [<Literal>]
    let EditorVariable = "EDITOR"

    [<Literal>]
    let SearchPathVariable = "PATH"

    let current () =
        { Platform = if OperatingSystem.IsWindows() then Windows else Unix
          UserProfile = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
          ApplicationData = Environment.GetFolderPath Environment.SpecialFolder.ApplicationData
          DataHome = Environment.GetEnvironmentVariable DataHomeVariable
          DataDirectories = Environment.GetEnvironmentVariable DataDirectoriesVariable
          ConfigHome = Environment.GetEnvironmentVariable ConfigHomeVariable
          ConfigDirectories = Environment.GetEnvironmentVariable ConfigDirectoriesVariable
          TemporaryRoot = IO.Path.GetTempPath()
          Session = Guid.NewGuid().ToString "N"
          SearchPath = Environment.GetEnvironmentVariable SearchPathVariable
          Editor = Environment.GetEnvironmentVariable EditorVariable |> Option.ofObj }

    let variable (name: string) =
        match Environment.GetEnvironmentVariable name with
        | null
        | "" -> None
        | value -> Some value
