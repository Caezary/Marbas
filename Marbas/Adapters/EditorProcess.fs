module Marbas.Adapters.EditorProcess

open System.Diagnostics
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.App

type EditorWait =
    | TakesTheTerminal
    | StartsDetached

type EditorStart =
    { Start: ProcessStartInfo
      Wait: EditorWait }

let private waitOn platform =
    match platform with
    | Windows -> StartsDetached
    | Unix -> TakesTheTerminal

let describe (fs: IFileSystem) (configured: string option) platform (chosen: ProgramPath option) path =
    let editor = chosen |> Option.map ProgramPath.value |> Option.orElse configured
    let wait = waitOn platform
    let start = ProcessStartInfo(EditorCommand.choose editor platform)
    start.ArgumentList.Add(FileSystemPath.value path)
    start.WorkingDirectory <- FileSystemPath.value (FileSystemPath.directory fs path)
    start.UseShellExecute <- (wait = StartsDetached)
    { Start = start; Wait = wait }

let private startDetached (start: ProcessStartInfo) =
    match Process.Start start with
    | null -> ()
    | started -> started.Dispose()

let private startAndWait (start: ProcessStartInfo) () =
    match Process.Start start with
    | null -> ()
    | started ->
        use started = started
        started.WaitForExit()

let run
    (fs: IFileSystem)
    (handOver: (unit -> unit) -> unit)
    (configured: string option)
    platform
    (chosen: ProgramPath option)
    path
    =
    let editor = describe fs configured platform chosen path

    try
        match editor.Wait with
        | StartsDetached -> startDetached editor.Start
        | TakesTheTerminal -> handOver (startAndWait editor.Start)

        Ok()
    with ex ->
        Error(Operations.mapException path ex)
