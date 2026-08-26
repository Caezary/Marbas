namespace Marbas.Adapters

open System
open Marbas

type HostEnvironment =
    { Platform: Platform
      UserProfile: string
      ApplicationData: string
      DataHome: string
      Editor: string option }

module HostEnvironment =

    [<Literal>]
    let DataHomeVariable = "XDG_DATA_HOME"

    [<Literal>]
    let EditorVariable = "EDITOR"

    let current () =
        { Platform = if OperatingSystem.IsWindows() then Windows else Unix
          UserProfile = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
          ApplicationData = Environment.GetFolderPath Environment.SpecialFolder.ApplicationData
          DataHome = Environment.GetEnvironmentVariable DataHomeVariable
          Editor = Environment.GetEnvironmentVariable EditorVariable |> Option.ofObj }
