namespace Marbas.FileSystem

type Elevation =
    | AsUser
    | AsAdministrator

type LaunchRequest =
    { Target: FileSystemPath
      Program: ProgramPath option
      WorkingDirectory: FileSystemPath
      Elevation: Elevation }

type LaunchError =
    | NothingAssociated of target: FileSystemPath
    | ProgramNotFound of program: ProgramPath
    | LaunchRefused of target: FileSystemPath * reason: string
    | ElevationDeclined of target: FileSystemPath
