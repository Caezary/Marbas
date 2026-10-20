module Marbas.Messages.CrashMessage

open Marbas.FileSystem

let written version path =
    $"{VersionMessage.product version} stopped on an unexpected error. The details are in '{FileSystemPath.value path}'."

let unwritten version error =
    match error with
    | LogAccessDenied path ->
        $"{VersionMessage.product version} stopped on an unexpected error and could not write it to '{FileSystemPath.value path}': access denied."
    | LogWriteFailed(path, reason) ->
        $"{VersionMessage.product version} stopped on an unexpected error and could not write it to '{FileSystemPath.value path}': {reason}"
