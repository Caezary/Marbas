namespace Marbas.Adapters

open System
open System.Reflection
open Marbas.Domain

type Logging =
    | FailuresOnly
    | EveryTransition

module Logging =

    [<Literal>]
    let DebugSwitch = "--debug"

    let ofArguments (arguments: string array) =
        if Array.contains DebugSwitch arguments then
            EveryTransition
        else
            FailuresOnly

type Launch =
    | OpenPanels
    | PrintVersion

module Launch =

    [<Literal>]
    let VersionSwitch = "--version"

    let ofArguments (arguments: string array) =
        if Array.contains VersionSwitch arguments then
            PrintVersion
        else
            OpenPanels

type HostEnvironment =
    { Launch: Launch
      Version: ProductVersion
      Platform: Platform
      UserProfile: string
      ApplicationData: string
      DataHome: string option
      DataDirectories: string option
      ConfigHome: string option
      ConfigDirectories: string option
      TemporaryRoot: string
      Session: string
      SearchPath: string option
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

    [<Literal>]
    let private UnknownVersion = "unknown"

    let private defaultProgramExtensions = [ ".COM"; ".EXE"; ".BAT"; ".CMD" ]

    let programExtensions (raw: string option) =
        let listed =
            (raw |> Option.defaultValue "").Split ';'
            |> Array.toList
            |> List.map _.Trim()
            |> List.filter ((<>) "")

        match listed with
        | [] -> defaultProgramExtensions
        | extensions -> extensions

    let variable (name: string) =
        match Environment.GetEnvironmentVariable name with
        | null
        | "" -> None
        | value -> Some value

    let productVersion () =
        match typeof<HostEnvironment>.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() with
        | null -> ProductVersion UnknownVersion
        | stamped -> ProductVersion stamped.InformationalVersion

    let current (arguments: string array) =
        { Launch = Launch.ofArguments arguments
          Version = productVersion ()
          Platform = if OperatingSystem.IsWindows() then Windows else Unix
          UserProfile = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
          ApplicationData = Environment.GetFolderPath Environment.SpecialFolder.ApplicationData
          DataHome = variable DataHomeVariable
          DataDirectories = variable DataDirectoriesVariable
          ConfigHome = variable ConfigHomeVariable
          ConfigDirectories = variable ConfigDirectoriesVariable
          TemporaryRoot = IO.Path.GetTempPath()
          Session = Guid.NewGuid().ToString "N"
          SearchPath = variable SearchPathVariable
          ProgramExtensions = programExtensions (variable ProgramExtensionsVariable)
          Editor = variable EditorVariable
          Logging = Logging.ofArguments arguments }
