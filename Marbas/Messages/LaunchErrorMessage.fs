module Marbas.Messages.LaunchErrorMessage

open Marbas.Domain
open Marbas.FileSystem

[<Literal>]
let Title = "Cannot run file"

[<Literal>]
let CommandTitle = "Cannot run command"

[<Literal>]
let private OpenerPackage = "xdg-utils"

let describe error =
    match error with
    | NothingAssociated target -> $"Nothing on this system opens '{FileSystemPath.value target}'."
    | ProgramNotFound program -> $"'{ProgramPath.value program}' could not be found."
    | OpenerNotInstalled opener ->
        $"Nothing on this system opens files: '{ProgramPath.value opener}' is not installed, and the package {OpenerPackage} provides it."
    | LaunchRefused(target, reason) -> $"Could not start '{FileSystemPath.value target}': {reason}"
    | NotAProgram target ->
        $"'{FileSystemPath.value target}' is not a program: only a program can be started with administrator rights."
    | ElevationDeclined target -> $"'{FileSystemPath.value target}' was not started with administrator rights."
