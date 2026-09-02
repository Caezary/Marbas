module Marbas.Ui.Messages.LaunchErrorMessage

open Marbas.FileSystem

[<Literal>]
let Title = "Cannot run file"

let describe error =
    match error with
    | NothingAssociated target -> $"Nothing on this system opens '{FileSystemPath.value target}'."
    | ProgramNotFound program -> $"'{ProgramPath.value program}' could not be found."
    | LaunchRefused(target, reason) -> $"Could not start '{FileSystemPath.value target}': {reason}"
    | ElevationDeclined target -> $"'{FileSystemPath.value target}' was not started with administrator rights."
