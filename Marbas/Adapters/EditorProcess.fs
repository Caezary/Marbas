module Marbas.Adapters.EditorProcess

open System.Diagnostics
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.App

type EditorWait =
    | WaitsForExit
    | StartsDetached

type EditorStart =
    { Start: ProcessStartInfo
      Wait: EditorWait }

let private waitOn platform =
    match platform with
    | Windows -> StartsDetached
    | Unix -> WaitsForExit

let describe (fs: IFileSystem) (configured: string option) platform (chosen: ProgramPath option) path =
    let editor = chosen |> Option.map ProgramPath.value |> Option.orElse configured
    let wait = waitOn platform
    let start = ProcessStartInfo(EditorCommand.choose editor platform)
    start.ArgumentList.Add(FileSystemPath.value path)
    start.WorkingDirectory <- FileSystemPath.value (FileSystemPath.directory fs path)
    start.UseShellExecute <- (wait = StartsDetached)
    { Start = start; Wait = wait }

let run
    (fs: IFileSystem)
    (repaint: unit -> unit)
    (configured: string option)
    platform
    (chosen: ProgramPath option)
    path
    =
    let editor = describe fs configured platform chosen path

    try
        match Process.Start editor.Start, editor.Wait with
        | null, _ -> ()
        | started, StartsDetached -> started.Dispose()
        | started, WaitsForExit ->
            use started = started
            started.WaitForExit()
            repaint ()

        Ok()
    with ex ->
        Error(Operations.mapException path ex)
