namespace Marbas.FileSystem

open Marbas.Domain

type Elevation =
    | AsUser
    | AsAdministrator

module Elevation =

    let text elevation =
        match elevation with
        | AsUser -> "user"
        | AsAdministrator -> "administrator"

    let tryParse (text: string) =
        match text.Trim().ToLowerInvariant() with
        | "user" -> Some AsUser
        | "administrator" -> Some AsAdministrator
        | _ -> None

type LaunchRequest =
    { Target: FileSystemPath
      Program: ProgramPath option
      WorkingDirectory: FileSystemPath
      Elevation: Elevation }

type LaunchError =
    | NothingAssociated of target: FileSystemPath
    | ProgramNotFound of program: ProgramPath
    | OpenerNotInstalled of opener: ProgramPath
    | LaunchRefused of target: FileSystemPath * reason: string
    | NotAProgram of target: FileSystemPath
    | ElevationDeclined of target: FileSystemPath
