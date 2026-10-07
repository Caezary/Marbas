module Marbas.Adapters.CommandProcess

open System.ComponentModel
open System.Diagnostics
open System.IO.Abstractions
open FsToolkit.ErrorHandling
open Marbas.Domain
open Marbas.FileSystem

[<Literal>]
let WindowsShell = "cmd.exe"

[<Literal>]
let UnixShell = "/bin/sh"

[<Literal>]
let private FileNotFound = 2

[<Literal>]
let private PromptCancelled = 1223

let private programPath (run: CommandRun) =
    FileSystemPath.create run.Command.Program.Text

let private argumentTexts (run: CommandRun) =
    run.Command.Arguments |> List.map _.Text

let private directProcess (command: string) arguments =
    let start = ProcessStartInfo command
    arguments |> List.iter start.ArgumentList.Add
    start

let private quotedForSh (word: CommandWord) =
    match word.Quoting with
    | Bare -> word.Text
    | Quoted -> "'" + word.Text.Replace("'", @"'\''") + "'"

let private shLine (run: CommandRun) =
    ExpandedCommand.words run.Command |> List.map quotedForSh |> String.concat " "

let private cmdSwitch way =
    match way with
    | InShellKeptOpen -> "/k"
    | InShellClosing
    | Directly -> "/c"

let private cmdLine (run: CommandRun) =
    let line = ExpandedCommand.spelled run.Command

    match run.Elevation with
    | AsAdministrator -> $"cd /d \"{FileSystemPath.value run.WorkingDirectory}\" && {line}"
    | AsUser -> line

let private throughCmd (run: CommandRun) =
    ProcessStartInfo(WindowsShell, $"/s {cmdSwitch run.Run} \"{cmdLine run}\"")

let private onWindows (run: CommandRun) =
    let start =
        match run.Run with
        | Directly -> directProcess run.Command.Program.Text (argumentTexts run)
        | InShellClosing
        | InShellKeptOpen -> throughCmd run

    start.UseShellExecute <- true

    match run.Elevation with
    | AsAdministrator -> start.Verb <- LaunchProcess.ElevatedVerb
    | AsUser -> ()

    start

let private onUnix (run: CommandRun) =
    match run.Run with
    | Directly -> LaunchProcess.detached run.Command.Program.Text (argumentTexts run)
    | InShellClosing
    | InShellKeptOpen -> LaunchProcess.detached UnixShell [ "-c"; shLine run ]

let private startingIn (run: CommandRun) (start: ProcessStartInfo) =
    start.WorkingDirectory <- FileSystemPath.value run.WorkingDirectory
    start

let describe platform (run: CommandRun) =
    match platform, run.Elevation with
    | Unix, AsAdministrator -> Error(LaunchRefused(programPath run, LaunchProcess.ElevationUnavailable))
    | Windows, _ -> Ok(startingIn run (onWindows run))
    | Unix, AsUser -> Ok(startingIn run (onUnix run))

let writeList (fs: IFileSystem) (listFile: ListFile) =
    try
        fs.Directory.CreateDirectory(FileSystemPath.value (FileSystemPath.directory fs listFile.FilePath))
        |> ignore

        fs.File.WriteAllLines(FileSystemPath.value listFile.FilePath, listFile.Listed |> List.map FileSystemPath.value)
        Ok()
    with ex ->
        Error(LaunchRefused(listFile.FilePath, ex.Message))

let private notFound (run: CommandRun) (refusal: Win32Exception) =
    ProgramPath.tryCreate run.Command.Program.Text
    |> Option.map ProgramNotFound
    |> Option.defaultValue (LaunchRefused(programPath run, refusal.Message))

let private failure (run: CommandRun) (ex: exn) =
    match ex with
    | :? Win32Exception as refusal ->
        match refusal.NativeErrorCode with
        | PromptCancelled -> ElevationDeclined(programPath run)
        | FileNotFound -> notFound run refusal
        | _ -> LaunchRefused(programPath run, refusal.Message)
    | _ -> LaunchRefused(programPath run, ex.Message)

let private launched (run: CommandRun) (start: ProcessStartInfo) =
    try
        match Process.Start start with
        | null -> ()
        | child -> if start.RedirectStandardOutput then LaunchProcess.drained child

        Ok()
    with ex ->
        Error(failure run ex)

let private listWritten (fs: IFileSystem) (run: CommandRun) =
    run.Command.ListFile |> Option.map (writeList fs) |> Option.defaultValue (Ok())

let start (fs: IFileSystem) platform (run: CommandRun) =
    result {
        let! described = describe platform run
        do! listWritten fs run
        return! launched run described
    }
