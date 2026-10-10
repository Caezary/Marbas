namespace Marbas.Adapters

open System
open Marbas.Domain

type Logging =
    | CrashesOnly
    | EveryTransition

module Logging =

    [<Literal>]
    let DebugSwitch = "--debug"

    let ofArguments (arguments: string array) =
        if Array.contains DebugSwitch arguments then
            EveryTransition
        else
            CrashesOnly

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
      ProgramExtensions: string list
      Editor: string option
      Logging: Logging }

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

    [<Literal>]
    let ProgramExtensionsVariable = "PATHEXT"

    let private defaultProgramExtensions = [ ".COM"; ".EXE"; ".BAT"; ".CMD" ]

    let programExtensions (raw: string) =
        let listed =
            (Option.ofObj raw |> Option.defaultValue "").Split ';'
            |> Array.toList
            |> List.map _.Trim()
            |> List.filter ((<>) "")

        match listed with
        | [] -> defaultProgramExtensions
        | extensions -> extensions

    let current (arguments: string array) =
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
          ProgramExtensions = programExtensions (Environment.GetEnvironmentVariable ProgramExtensionsVariable)
          Editor = Environment.GetEnvironmentVariable EditorVariable |> Option.ofObj
          Logging = Logging.ofArguments arguments }

    let variable (name: string) =
        match Environment.GetEnvironmentVariable name with
        | null
        | "" -> None
        | value -> Some value
