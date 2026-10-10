module Marbas.Adapters.EditorProcess

open System.Diagnostics
open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem

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

let private starting (editor: string) (path: FileSystemPath) =
    let start = ProcessStartInfo editor
    start.ArgumentList.Add(FileSystemPath.value path)
    start

let private throughShell editor path =
    let start = starting editor path
    start.UseShellExecute <- true
    start

let private onWindows windowProgram directory editor path =
    match windowProgram directory editor with
    | Some program -> LaunchProcess.detached (FileSystemPath.value program) [ FileSystemPath.value path ]
    | None -> throughShell editor path

let describe (fs: IFileSystem) (configured: string option) platform windowProgram (chosen: ProgramPath option) path =
    let named = chosen |> Option.map ProgramPath.value |> Option.orElse configured
    let editor = EditorCommand.choose named platform
    let directory = FileSystemPath.directory fs path

    let start =
        match platform with
        | Windows -> onWindows windowProgram directory editor path
        | Unix -> starting editor path

    start.WorkingDirectory <- FileSystemPath.value directory

    { Start = start
      Wait = waitOn platform }

let private startDetached (start: ProcessStartInfo) =
    match Process.Start start with
    | null -> ()
    | started ->
        if start.RedirectStandardOutput then
            LaunchProcess.drained started

        started.Dispose()

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
    windowProgram
    (chosen: ProgramPath option)
    path
    =
    let editor = describe fs configured platform windowProgram chosen path

    try
        match editor.Wait with
        | StartsDetached -> startDetached editor.Start
        | TakesTheTerminal -> handOver (startAndWait editor.Start)

        Ok()
    with ex ->
        Error(Operations.mapException path ex)
