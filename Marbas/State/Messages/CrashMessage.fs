module Marbas.State.Messages.CrashMessage

open Marbas.FileSystem

let written path =
    $"Marbas stopped on an unexpected error. The details are in '{FileSystemPath.value path}'."

let unwritten error =
    match error with
    | LogAccessDenied path ->
        $"Marbas stopped on an unexpected error and could not write it to '{FileSystemPath.value path}': access denied."
    | LogWriteFailed(path, reason) ->
        $"Marbas stopped on an unexpected error and could not write it to '{FileSystemPath.value path}': {reason}"
